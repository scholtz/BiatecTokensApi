# ARC76 Backend Reliability and Error Handling Guide

## Overview

This guide documents the backend reliability features, error handling patterns, and recovery procedures for the BiatecTokensApi with focus on ARC76 account lifecycle management.

**Last Updated:** 2026-02-17  
**Version:** 1.0  
**Authors:** Backend Reliability Sprint Team

---

## Table of Contents

1. [Error Handling Architecture](#error-handling-architecture)
2. [ARC76 Account Lifecycle](#arc76-account-lifecycle)
3. [Key Management and Recovery](#key-management-and-recovery)
4. [Authentication Error Scenarios](#authentication-error-scenarios)
5. [Deployment Orchestration Reliability](#deployment-orchestration-reliability)
6. [Troubleshooting Guide](#troubleshooting-guide)
7. [API Error Response Format](#api-error-response-format)

---

## Error Handling Architecture

### Standardized Error Response Format

All API errors return a consistent structure defined by `ApiErrorResponse`:

```json
{
  "success": false,
  "errorCode": "ACCOUNT_NOT_READY",
  "errorMessage": "Account is not ready: Degraded",
  "remediationHint": "Your account is experiencing issues. Please contact support for assistance.",
  "correlationId": "0HNJEEGM9JONN",
  "path": "/api/v1/token/deploy",
  "timestamp": "2026-02-17T22:15:30.123Z",
  "details": {
    "reason": "Key provider validation failed"
  }
}
```

**Key Fields:**
- `success`: Always `false` for errors
- `errorCode`: Machine-readable error code from `ErrorCodes` class
- `errorMessage`: Human-readable error description
- `remediationHint`: Actionable guidance for the user or developer
- `correlationId`: Unique identifier for tracing requests across services
- `details`: Optional additional context for debugging

### Error Categories

The system organizes errors into categories for consistent handling:

1. **Validation Errors (400)** - Invalid request parameters
2. **Authentication Errors (401, 403)** - Invalid credentials, expired tokens
3. **Resource Errors (404, 409)** - Not found, conflicts
4. **External Service Errors (502, 503, 504)** - Blockchain, IPFS, key vault issues
5. **Blockchain-Specific Errors (422)** - Insufficient funds, transaction failures
6. **Rate Limiting (429)** - Too many requests
7. **Internal Errors (500)** - Unexpected system errors

### Using ErrorResponseHelper

The `ErrorResponseHelper` utility provides methods for creating standardized error responses:

```csharp
// Create authentication error
var error = ErrorResponseHelper.CreateAuthenticationError(
    ErrorCodes.INVALID_CREDENTIALS,
    "Invalid email or password",
    correlationId: HttpContext.TraceIdentifier
);

// Create key management error
var error = ErrorResponseHelper.CreateKeyManagementError(
    "Azure Key Vault connection failed",
    correlationId: correlationId
);

// Create account readiness error
var error = ErrorResponseHelper.CreateAccountReadinessError(
    "Degraded",
    reason: "Mnemonic decryption failed",
    correlationId: correlationId
);
```

---

## ARC76 Account Lifecycle

### Account States

ARC76 accounts progress through the following states:

1. **NotInitialized** - Account doesn't exist yet
2. **Initializing** - Account creation in progress
3. **Ready** - Account fully operational
4. **Degraded** - Account functional but experiencing issues
5. **Failed** - Account initialization or operation failed

### State Transitions

```
NotInitialized → Initializing → Ready
                        ↓         ↓
                    Failed   Degraded
```

### Deterministic Behavior Guarantees

**Invariant 1:** Same credentials always produce same Algorand address

```csharp
// Test: LoginMultipleTimes_ReturnsSameAlgorandAddress()
var address1 = await LoginAndGetAddress(email, password);
var address2 = await LoginAndGetAddress(email, password);
Assert.That(address1, Is.EqualTo(address2));
```

**Invariant 2:** Password change does NOT change Algorand address

```csharp
// Mnemonic is encrypted with system key, not user password
var addressBefore = await GetUserAddress(userId);
await ChangePassword(userId, oldPassword, newPassword);
var addressAfter = await GetUserAddress(userId);
Assert.That(addressBefore, Is.EqualTo(addressAfter));
```

**Invariant 3:** Each user has a unique Algorand address

```csharp
// BIP39 mnemonic generation ensures cryptographic uniqueness
var user1 = await Register("user1@example.com", "Password123!");
var user2 = await Register("user2@example.com", "Password123!");
Assert.That(user1.AlgorandAddress, Is.Not.EqualTo(user2.AlgorandAddress));
```

---

## Key Management and Recovery

### Key Provider Configuration

The system supports multiple key providers for mnemonic encryption:

| Provider | Use Case | Configuration |
|----------|----------|---------------|
| **AzureKeyVault** | Production (Azure) | Vault URL, Key Name |
| **AwsKms** | Production (AWS) | KMS Key ID, Region |
| **EnvironmentVariable** | Staging/Development | Environment variable name |
| **Hardcoded** | Testing ONLY | Hardcoded key value |

**Configuration Example:**

```json
{
  "KeyManagementConfig": {
    "Provider": "AzureKeyVault",
    "AzureKeyVault": {
      "VaultUrl": "https://myvault.vault.azure.net/",
      "KeyName": "mnemonic-encryption-key"
    }
  }
}
```

### Key Provider Error Handling

The `AuthenticationService` validates key provider configuration before attempting decryption:

```csharp
private async Task<string> DecryptMnemonicForSigning(string encryptedMnemonic)
{
    try
    {
        var keyProvider = _keyProviderFactory.CreateProvider();
        
        // Validate provider configuration before accessing keys
        var isConfigValid = await keyProvider.ValidateConfigurationAsync();
        if (!isConfigValid)
        {
            throw new InvalidOperationException(
                $"Key provider '{keyProvider.ProviderType}' is not properly configured.");
        }
        
        var systemPassword = await keyProvider.GetEncryptionKeyAsync();
        return DecryptMnemonic(encryptedMnemonic, systemPassword);
    }
    catch (InvalidOperationException)
    {
        // Re-throw with user-friendly message
        throw new InvalidOperationException(
            "Unable to access encryption keys. Please contact support.");
    }
}
```

### Recovery Procedures

#### Scenario 1: Key Provider Unavailable

**Symptoms:**
- Error code: `CONFIGURATION_ERROR`
- Error message: "Unable to access encryption keys"

**Recovery Steps:**
1. Check key provider connectivity (Azure Key Vault, AWS KMS)
2. Verify service principal/IAM permissions
3. Check key vault firewall rules
4. Review key provider logs in cloud console
5. Escalate to infrastructure team if connectivity restored but issue persists

**Monitoring:**
```bash
# Check key provider health
GET /health
# Returns degraded status if key provider fails validation
```

#### Scenario 2: Encrypted Mnemonic Missing

**Symptoms:**
- Error code: `ACCOUNT_NOT_READY`
- Error message: "Account credentials are missing"

**Recovery Steps:**
1. Verify user record exists in database
2. Check if `EncryptedMnemonic` field is populated
3. If missing: User must create new account (old account cannot be recovered)
4. Document incident for security review

**Prevention:**
- Database-level NOT NULL constraints on `EncryptedMnemonic`
- Pre-insert validation in repository layer
- Backup/replication for user data

#### Scenario 3: Decryption Failure

**Symptoms:**
- Error code: `ACCOUNT_NOT_READY`
- Error message: "Unable to decrypt account credentials"

**Possible Causes:**
1. System encryption key rotated without re-encrypting mnemonics
2. Data corruption in `EncryptedMnemonic` field
3. Wrong key provider configured

**Recovery Steps:**
1. Check key provider configuration matches what was used for encryption
2. Verify key rotation schedule and re-encryption status
3. Restore from backup if data corruption suspected
4. If irrecoverable: User must create new account

---

## Authentication Error Scenarios

### Account Lockout

**Trigger:** 5 failed login attempts within 30 minutes

**Error Response:**
```json
{
  "success": false,
  "errorCode": "ACCOUNT_LOCKED",
  "errorMessage": "Account is locked due to too many failed login attempts.",
  "remediationHint": "Your account will be unlocked in approximately 25 minutes. Contact support to unlock immediately.",
  "details": {
    "lockoutEnd": "2026-02-17T23:00:00Z",
    "minutesRemaining": 25
  }
}
```

**User Actions:**
1. Wait for automatic unlock (30 minutes)
2. Contact support for immediate unlock
3. Use password reset if credentials forgotten

**Admin Actions:**
```csharp
// Manual unlock (to be implemented in admin panel)
await _userRepository.UnlockUserAsync(userId);
```

### Password Strength Validation

**Requirements:**
- Minimum 8 characters
- At least one uppercase letter
- At least one lowercase letter
- At least one number
- At least one special character

**Error Response:**
```json
{
  "success": false,
  "errorCode": "WEAK_PASSWORD",
  "errorMessage": "Password must be at least 8 characters and contain uppercase, lowercase, number, and special character"
}
```

### Refresh Token Errors

| Error Code | Meaning | User Action |
|------------|---------|-------------|
| `INVALID_REFRESH_TOKEN` | Token not found or malformed | Re-authenticate with email/password |
| `REFRESH_TOKEN_EXPIRED` | Token past expiration date | Re-authenticate with email/password |
| `REFRESH_TOKEN_REVOKED` | Token manually revoked (e.g., password change) | Re-authenticate with email/password |

---

## Deployment Orchestration Reliability

### Deployment State Machine

```
Queued → Submitted → Pending → Confirmed → Indexed → Completed
  ↓         ↓          ↓          ↓          ↓          ↓
  └──────────────── Failed ─────────────────────────────┘
                      ↓
                   Queued (retry)
```

### Retry Logic

Deployment errors are categorized by retryability:

| Category | Retryable | Retry Delay |
|----------|-----------|-------------|
| NetworkError | Yes | 30 seconds |
| ValidationError | No | N/A |
| ComplianceError | No | N/A |
| InsufficientFunds | Yes (after funding) | 60 seconds |
| TransactionFailure | Yes | 60 seconds |
| RateLimitExceeded | Yes | Dynamic (from headers) |
| InternalError | Yes | 120 seconds |

### Idempotency

All deployment endpoints support idempotency via the `Idempotency-Key` header:

```http
POST /api/v1/token/deploy/asa
Idempotency-Key: 550e8400-e29b-41d4-a716-446655440000
Content-Type: application/json

{
  "name": "MyToken",
  "symbol": "MTK"
}
```

**Behavior:**
- Same key + same request = returns cached response
- Same key + different request = returns `IDEMPOTENCY_KEY_MISMATCH` error
- Cache TTL: 24 hours
- SHA256 hash validation of request parameters

---

## Troubleshooting Guide

### Common Issues

#### Issue: "Unable to access encryption keys"

**Check:**
1. Key provider configuration
2. Network connectivity to key vault
3. Service principal permissions
4. Key vault firewall rules

**Quick Fix:**
```bash
# Test key vault connectivity
az keyvault secret show --vault-name <vault-name> --name <key-name>

# Check service principal permissions
az keyvault get-policy --vault-name <vault-name> --spn <client-id>
```

#### Issue: "Account is not ready: Degraded"

**Check:**
1. Recent key rotations
2. Database consistency
3. Key provider logs

**Quick Fix:**
```csharp
// Run account integrity check
var readiness = await _arc76ReadinessService.CheckAccountReadinessAsync(userId);
if (readiness.State == ARC76ReadinessState.Degraded)
{
    // Check readiness.Reason for specifics
    _logger.LogWarning("Account degraded: {Reason}", readiness.Reason);
}
```

#### Issue: Deployment stuck in "Pending" state

**Check:**
1. Blockchain network status
2. Transaction hash in block explorer
3. Gas price and network congestion

**Quick Fix:**
```csharp
// Get deployment status
var deployment = await _deploymentStatusService.GetDeploymentAsync(deploymentId);
if (deployment.Status == DeploymentStatus.Pending)
{
    // Check transaction on blockchain
    var txn = await _algoClient.GetTransactionByIdAsync(deployment.TransactionHash);
    // Take appropriate action based on txn status
}
```

### Logging and Diagnostics

All critical operations include structured logging with correlation IDs:

```csharp
_logger.LogInformation(
    "User registered successfully: Email={Email}, AlgorandAddress={Address}, CorrelationId={CorrelationId}",
    LoggingHelper.SanitizeLogInput(user.Email),
    LoggingHelper.SanitizeLogInput(user.AlgorandAddress),
    correlationId
);
```

**Log Query Examples:**

```bash
# Find all errors for a specific user
grep "UserId=550e8400-e29b-41d4-a716-446655440000" logs/*.log | grep "ERROR"

# Track request across services by correlation ID
grep "CorrelationId=0HNJEEGM9JONN" logs/*.log

# Find all key provider errors
grep "Key provider" logs/*.log | grep "ERROR"
```

---

## API Error Response Format

### Standard Error Codes

See `ErrorCodes.cs` for complete list. Key codes:

**Authentication (401, 403):**
- `UNAUTHORIZED` - Not authenticated
- `INVALID_AUTH_TOKEN` - Token expired or invalid
- `ACCOUNT_LOCKED` - Too many failed login attempts
- `ACCOUNT_INACTIVE` - Account disabled
- `INVALID_CREDENTIALS` - Wrong email/password

**Validation (400):**
- `INVALID_REQUEST` - Invalid parameters
- `MISSING_REQUIRED_FIELD` - Required field missing
- `WEAK_PASSWORD` - Password doesn't meet requirements

**Resource (404, 409):**
- `NOT_FOUND` - Resource doesn't exist
- `ALREADY_EXISTS` - Duplicate resource
- `CONFLICT` - State conflict

**System (500, 502, 503):**
- `INTERNAL_SERVER_ERROR` - Unexpected error
- `CONFIGURATION_ERROR` - System misconfigured
- `BLOCKCHAIN_CONNECTION_ERROR` - Can't reach blockchain
- `CIRCUIT_BREAKER_OPEN` - Service temporarily unavailable

**Blockchain (422):**
- `INSUFFICIENT_FUNDS` - Not enough funds for transaction
- `TRANSACTION_FAILED` - Transaction rejected
- `CONTRACT_EXECUTION_FAILED` - Smart contract error

### Error Response Examples

#### Successful Response
```json
{
  "success": true,
  "userId": "550e8400-e29b-41d4-a716-446655440000",
  "email": "user@example.com",
  "algorandAddress": "WPHUHJYADS64FWZW74IMSY5RYSJNHSVF56EHQMXMAUC6HAK46R...",
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresAt": "2026-02-17T23:15:30Z"
}
```

#### Error Response with Remediation
```json
{
  "success": false,
  "errorCode": "INSUFFICIENT_FUNDS",
  "errorMessage": "Insufficient funds to complete the operation.",
  "remediationHint": "Please add funds to your account and try again.",
  "correlationId": "0HNJEEGM9JONO",
  "path": "/api/v1/token/deploy/asa",
  "timestamp": "2026-02-17T22:15:30.456Z",
  "details": {
    "required": "1.5 ALGO",
    "available": "0.3 ALGO"
  }
}
```

---

## Support and Escalation

### Support Tiers

**Tier 1: Self-Service**
- Review error message and remediation hint
- Check troubleshooting guide
- Review API documentation

**Tier 2: Technical Support**
- Provide correlation ID from error response
- Include timestamp of issue
- Describe user journey leading to error

**Tier 3: Engineering Escalation**
- Persistent issues after Tier 2 support
- Key provider infrastructure problems
- Data integrity issues

### Incident Response

For production incidents:

1. **Identify:** Error code, correlation ID, affected users
2. **Assess:** Scope (single user vs. system-wide)
3. **Mitigate:** Failover, rollback, or temporary workaround
4. **Resolve:** Root cause fix
5. **Document:** Post-mortem and preventive measures

---

## Appendix

### Test Coverage

Error handling is validated by comprehensive test suites:

- `AuthenticationServiceErrorHandlingTests.cs` - 13 tests covering key provider failures, account lockout, password validation
- `ARC76CredentialDerivationTests.cs` - Deterministic behavior validation
- `ARC76EdgeCaseAndNegativeTests.cs` - Error scenario coverage
- `ARC76AccountReadinessServiceTests.cs` - Lifecycle state management

### Related Documentation

- [ARC76 Lifecycle Verification Strategy](BACKEND_ARC76_LIFECYCLE_VERIFICATION_STRATEGY.md)
- [Key Management Guide](KEY_MANAGEMENT_GUIDE.md)
- [Deployment Orchestration Guide](DEPLOYMENT_ORCHESTRATION_EXECUTIVE_SUMMARY.md)
- [API Documentation](README.md)

---

**Document Version:** 1.0  
**Last Updated:** 2026-02-17  
**Next Review:** 2026-03-17  
**Owner:** Backend Reliability Team

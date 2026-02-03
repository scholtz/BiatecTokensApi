# Backend API Stability Guide

## Overview

The Biatec Tokens API has been stabilized for MVP production deployment with comprehensive error handling, health monitoring, correlation-based tracing, and subscription tier validation. This document describes the stability features that ensure reliable, predictable, and secure API operations for regulated asset issuance.

## Key Stability Features

### 1. Standardized Error Responses

All API endpoints return consistent, structured error responses that include:

#### Error Response Structure

```json
{
  "success": false,
  "errorCode": "TRANSACTION_FAILED",
  "errorMessage": "The transaction could not be completed",
  "remediationHint": "Verify your account balance and transaction parameters, then try again.",
  "details": {
    "operation": "token-deployment",
    "additionalInfo": "Gas estimation failed"
  },
  "timestamp": "2026-02-03T19:00:00Z",
  "path": "/api/v1/token/erc20-mintable/create",
  "correlationId": "abc123-def456-789"
}
```

#### Standard Error Codes

The API uses a comprehensive set of error codes for programmatic error handling:

**Validation Errors (400)**
- `INVALID_REQUEST` - Invalid request parameters
- `MISSING_REQUIRED_FIELD` - Required field is missing
- `INVALID_NETWORK` - Network configuration is invalid
- `INVALID_TOKEN_PARAMETERS` - Token parameters are invalid

**Authentication/Authorization (401/403)**
- `UNAUTHORIZED` - Authentication required
- `FORBIDDEN` - Access denied
- `INVALID_AUTH_TOKEN` - Authentication token is invalid

**Resource Errors (404/409)**
- `NOT_FOUND` - Resource not found
- `ALREADY_EXISTS` - Resource already exists
- `CONFLICT` - Resource conflict

**Blockchain Errors (422)**
- `INSUFFICIENT_FUNDS` - Insufficient balance
- `TRANSACTION_FAILED` - Transaction failed
- `CONTRACT_EXECUTION_FAILED` - Smart contract execution failed
- `GAS_ESTIMATION_FAILED` - Gas estimation failed

**External Service Errors (502/503/504)**
- `BLOCKCHAIN_CONNECTION_ERROR` - Cannot connect to blockchain
- `IPFS_SERVICE_ERROR` - IPFS service unavailable
- `EXTERNAL_SERVICE_ERROR` - External service error
- `TIMEOUT` - Operation timed out
- `CIRCUIT_BREAKER_OPEN` - Circuit breaker is open

**Subscription Errors (402/429)**
- `SUBSCRIPTION_LIMIT_REACHED` - Subscription tier limit reached
- `RATE_LIMIT_EXCEEDED` - Rate limit exceeded

**Server Errors (500)**
- `INTERNAL_SERVER_ERROR` - Unexpected server error
- `CONFIGURATION_ERROR` - Configuration error

### 2. Correlation IDs for Request Tracing

Every API request and response includes a `correlationId` that uniquely identifies the request across all systems and logs.

#### Using Correlation IDs

**In Requests:**
The correlation ID is automatically generated from the HTTP context's `TraceIdentifier`.

**In Responses:**
All successful and error responses include the correlation ID:

```json
{
  "success": true,
  "deploymentId": "deploy-123",
  "correlationId": "abc123-def456-789"
}
```

**In Logs:**
All log entries include the correlation ID for tracing:

```
[2026-02-03 19:00:00] INFO: Token deployed successfully. CorrelationId: abc123-def456-789
```

**Best Practices:**
- Include the correlation ID when reporting issues to support
- Use correlation IDs to trace requests across distributed systems
- Search logs by correlation ID to investigate specific requests

### 3. Health Monitoring

The API provides multiple health check endpoints for monitoring and orchestration:

#### Health Endpoints

**Basic Health Check** (`/health`)
- Quick status check
- Returns 200 OK if healthy, 503 if unhealthy
- Used for basic uptime monitoring

**Readiness Probe** (`/health/ready`)
- Kubernetes readiness check
- Verifies all dependencies are available
- Returns 200 when ready to serve traffic

**Liveness Probe** (`/health/live`)
- Kubernetes liveness check
- Verifies the application is running
- Doesn't check external dependencies
- Returns 200 if process is alive

**Detailed Status** (`/api/v1/status`)
- Comprehensive API status with component health
- Returns detailed information about all dependencies
- Includes response times and degradation warnings

#### Status Endpoint Response

```json
{
  "version": "1.0.0",
  "buildTime": "2026-02-03",
  "timestamp": "2026-02-03T19:00:00Z",
  "uptime": "2.05:30:45",
  "environment": "Production",
  "status": "Healthy",
  "components": {
    "ipfs": {
      "status": "Healthy",
      "message": "IPFS API is reachable",
      "details": {
        "responseTimeMs": 150,
        "endpoint": "https://ipfs-api.biatec.io"
      }
    },
    "algorand": {
      "status": "Healthy",
      "message": "All Algorand networks are operational",
      "details": {
        "networks": {
          "mainnet": {
            "status": "healthy",
            "responseTimeMs": 200
          }
        }
      }
    },
    "evm": {
      "status": "Healthy",
      "message": "All EVM chains are operational",
      "details": {
        "chains": {
          "base-8453": {
            "status": "healthy",
            "responseTimeMs": 180
          }
        }
      }
    }
  }
}
```

#### Health Status Meanings

- **Healthy**: All systems operational
- **Degraded**: Some components slow or partially available
- **Unhealthy**: Critical components unavailable

### 4. Subscription Tier Validation

The API enforces subscription-based entitlements to enable tiered access and monetization.

#### Subscription Tiers

| Tier | Max Addresses/Asset | Bulk Operations | Audit Logs | Transfer Validation |
|------|---------------------|-----------------|------------|---------------------|
| Free | 10 | ❌ | ❌ | ✅ |
| Basic | 100 | ❌ | ✅ | ✅ |
| Premium | 1,000 | ✅ | ✅ | ✅ |
| Enterprise | Unlimited | ✅ | ✅ | ✅ |

#### Entitlement Error Response

When a user attempts an operation beyond their tier limits:

```json
{
  "success": false,
  "errorCode": "SUBSCRIPTION_LIMIT_REACHED",
  "errorMessage": "This feature requires a Premium or Enterprise subscription. Your current tier: Free.",
  "remediationHint": "Upgrade to Premium or Enterprise tier to access this feature. Visit the billing page to upgrade your subscription.",
  "details": {
    "currentTier": "Free",
    "requiredTier": "Premium",
    "featureType": "premium"
  },
  "timestamp": "2026-02-03T19:00:00Z",
  "correlationId": "abc123-def456-789",
  "path": "/api/v1/token/create"
}
```

#### Applying Tier Validation

The `SubscriptionTierValidation` attribute can be applied to controller actions:

```csharp
[SubscriptionTierValidation(RequiresPremium = true)]
[HttpPost("premium-feature")]
public async Task<IActionResult> PremiumFeature()
{
    // This endpoint requires Premium or Enterprise tier
}
```

### 5. Structured Logging

All critical operations are logged with structured data for monitoring and troubleshooting.

#### Log Structure

- **Level**: INFO, WARNING, ERROR
- **Message**: Human-readable description
- **Context**: Operation type, user, asset ID
- **Correlation ID**: Request tracking identifier
- **Timestamp**: UTC timestamp

#### Log Examples

**Successful Token Deployment:**
```
[INFO] Token deployed successfully at address 0x123... with transaction 0xabc... CorrelationId: xyz-789
```

**Subscription Tier Denial:**
```
[WARNING] Access denied for user ADDR123: Premium feature required. Current tier: Free. CorrelationId: xyz-789
```

**Transaction Failure:**
```
[ERROR] Token deployment failed: Insufficient funds. CorrelationId: xyz-789
```

### 6. Security Features

#### Log Injection Prevention

All user inputs are sanitized before logging to prevent log forging attacks:

```csharp
// WRONG - Vulnerable to log injection
_logger.LogInformation("User {UserId} requested {Action}", userId, action);

// RIGHT - Sanitized for security
_logger.LogInformation("User {UserId} requested {Action}", 
    LoggingHelper.SanitizeLogInput(userId), 
    LoggingHelper.SanitizeLogInput(action));
```

#### Authentication

All endpoints require ARC-0014 authentication:

```
Authorization: SigTx <base64-encoded-signed-transaction>
```

#### Sensitive Data Protection

- Secrets never logged or exposed in error messages
- Stack traces only in Development environment
- User data sanitized in all logs

## Best Practices for Clients

### 1. Error Handling

Always check the `success` field and handle errors appropriately:

```typescript
const response = await fetch('/api/v1/token/erc20-mintable/create', {
  method: 'POST',
  headers: {
    'Content-Type': 'application/json',
    'Authorization': authToken
  },
  body: JSON.stringify(tokenRequest)
});

const result = await response.json();

if (!result.success) {
  // Check error code for programmatic handling
  switch (result.errorCode) {
    case 'SUBSCRIPTION_LIMIT_REACHED':
      showUpgradePrompt(result.remediationHint);
      break;
    case 'INSUFFICIENT_FUNDS':
      showBalanceWarning(result.remediationHint);
      break;
    case 'BLOCKCHAIN_CONNECTION_ERROR':
      retryWithBackoff();
      break;
    default:
      showGenericError(result.errorMessage, result.correlationId);
  }
}
```

### 2. Idempotency

Use idempotency keys for token deployment to prevent duplicate operations:

```typescript
const idempotencyKey = generateUUID();

const response = await fetch('/api/v1/token/erc20-mintable/create', {
  method: 'POST',
  headers: {
    'Idempotency-Key': idempotencyKey,
    'Authorization': authToken
  },
  body: JSON.stringify(tokenRequest)
});
```

### 3. Health Monitoring

Implement health checks in your client application:

```typescript
async function checkAPIHealth() {
  const response = await fetch('/api/v1/status');
  const status = await response.json();
  
  if (status.status !== 'Healthy') {
    console.warn('API is degraded:', status);
    // Show warning to user or adjust behavior
  }
  
  return status;
}
```

### 4. Correlation ID Tracking

Always capture and store correlation IDs for troubleshooting:

```typescript
const result = await deployToken(request);

if (!result.success) {
  // Log error with correlation ID for support
  logError({
    operation: 'token-deployment',
    errorCode: result.errorCode,
    correlationId: result.correlationId,
    timestamp: result.timestamp
  });
  
  // Show user-friendly message with correlation ID
  showError(`Deployment failed: ${result.errorMessage}. 
    Reference ID: ${result.correlationId}`);
}
```

## Monitoring and Alerting

### Recommended Metrics

1. **Health Check Success Rate**
   - Monitor `/health/ready` endpoint
   - Alert if success rate drops below 95%

2. **Error Rate by Code**
   - Track error codes over time
   - Alert on unusual spikes in specific codes

3. **Response Times**
   - Monitor p50, p95, p99 latencies
   - Alert on degraded performance

4. **Subscription Tier Denials**
   - Track SUBSCRIPTION_LIMIT_REACHED errors
   - Use for product analytics and upgrade prompts

### Log Aggregation

Correlate logs across systems using correlation IDs:

```
# Example: Search logs by correlation ID
grep "CorrelationId: abc123-def456-789" /var/log/api/*.log
```

### Dashboard Examples

**API Health Dashboard:**
- Overall health status (Healthy/Degraded/Unhealthy)
- Component-level health (IPFS, Algorand, EVM)
- Response time graphs
- Error rate by code

**Operations Dashboard:**
- Token deployments per hour
- Success vs. failure rate
- Average deployment time
- Top error codes

**Business Metrics:**
- Subscription tier distribution
- Tier limit denials (upgrade opportunities)
- Feature usage by tier

## Troubleshooting

### Common Issues

**Issue: BLOCKCHAIN_CONNECTION_ERROR**
- Check network connectivity
- Verify RPC endpoint configuration
- Check blockchain node status

**Issue: SUBSCRIPTION_LIMIT_REACHED**
- Verify user's subscription tier
- Check current usage against tier limits
- Guide user to upgrade page

**Issue: TIMEOUT errors**
- Check external service health
- Review network latency
- Consider increasing timeout values

**Issue: IPFS_SERVICE_ERROR**
- Verify IPFS configuration
- Check IPFS service availability
- Test IPFS gateway connectivity

### Using Correlation IDs

1. User reports an issue
2. Collect correlation ID from error message
3. Search logs for correlation ID
4. Trace full request lifecycle
5. Identify root cause

### Support Workflow

1. **User Contact**: Collect correlation ID and timestamp
2. **Log Search**: Find all logs for correlation ID
3. **Context Gathering**: Review request/response details
4. **Root Cause**: Identify failure point
5. **Resolution**: Fix issue and respond to user

## Testing

### Unit Tests

The API includes comprehensive unit tests for:
- Error response consistency (9 tests)
- Correlation ID tracking
- Subscription tier validation
- Error code coverage

Run tests:
```bash
dotnet test --filter "FullyQualifiedName~BackendStabilityTests"
```

### Integration Tests

Existing integration tests cover:
- Health endpoint functionality
- Token deployment workflows
- Error handling scenarios
- Subscription enforcement

### Manual Testing

Test error scenarios:
```bash
# Test with missing authentication
curl -X POST https://api.example.com/api/v1/token/erc20-mintable/create \
  -H "Content-Type: application/json" \
  -d '{"name":"Test","symbol":"TST"}'

# Test health endpoints
curl https://api.example.com/health
curl https://api.example.com/api/v1/status
```

## Deployment Considerations

### Environment Variables

Required configuration:
- `App:Account` - Deployment account (use secrets manager)
- `IPFSConfig:*` - IPFS service configuration
- `EVMChains:*` - EVM chain RPC URLs
- `AlgorandAuthentication:*` - Network configurations

### Container Health Checks

Kubernetes health check configuration:
```yaml
livenessProbe:
  httpGet:
    path: /health/live
    port: 7000
  initialDelaySeconds: 10
  periodSeconds: 30

readinessProbe:
  httpGet:
    path: /health/ready
    port: 7000
  initialDelaySeconds: 5
  periodSeconds: 10
```

### Logging Configuration

Configure structured logging with correlation IDs:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    }
  }
}
```

## Summary

The Biatec Tokens API provides enterprise-grade stability through:

✅ **Consistent Error Responses** - Predictable error handling with remediation hints  
✅ **Correlation ID Tracking** - Full request traceability across systems  
✅ **Comprehensive Health Monitoring** - Real-time dependency status  
✅ **Subscription Tier Validation** - Automated entitlement enforcement  
✅ **Structured Logging** - Secure, searchable operational logs  
✅ **Security Features** - Log injection prevention and sensitive data protection  

These features ensure the API is reliable, predictable, and ready for regulated asset issuance in production environments.

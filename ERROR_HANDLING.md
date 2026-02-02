# Error Handling Guide

## Overview

The BiatecTokensApi implements comprehensive error handling to provide clear, actionable feedback when API operations fail. All errors follow a standardized response format for consistent client integration.

## Standardized Error Response Format

All API errors return a consistent JSON structure:

```json
{
  "success": false,
  "errorCode": "ERROR_CODE",
  "errorMessage": "Human-readable error description",
  "details": {
    "additionalInfo": "Optional debugging information"
  },
  "timestamp": "2026-02-02T09:30:00Z",
  "path": "/api/v1/token/erc20-mintable/create",
  "correlationId": "abc-123-def-456"
}
```

### Response Fields

- **success**: Always `false` for errors
- **errorCode**: Machine-readable error code for programmatic handling
- **errorMessage**: Human-readable explanation of the error
- **details**: Optional additional information (included in Development environment)
- **timestamp**: When the error occurred (UTC)
- **path**: API endpoint that generated the error
- **correlationId**: Unique ID for tracing requests across services

## Error Codes

### Validation Errors (HTTP 400)

| Error Code | Description | Common Causes |
|------------|-------------|---------------|
| `INVALID_REQUEST` | Request parameters are invalid | Missing required fields, invalid formats |
| `MISSING_REQUIRED_FIELD` | A required field is missing | Incomplete request body |
| `INVALID_NETWORK` | Blockchain network is invalid | Unsupported network name |
| `INVALID_TOKEN_PARAMETERS` | Token parameters are invalid | Invalid decimals, supply, or name |

**Example:**
```json
{
  "success": false,
  "errorCode": "INVALID_REQUEST",
  "errorMessage": "Token name is required",
  "timestamp": "2026-02-02T09:30:00Z"
}
```

### Authentication Errors (HTTP 401, 403)

| Error Code | Description | Resolution |
|------------|-------------|------------|
| `UNAUTHORIZED` | Authentication required | Provide valid ARC-0014 authentication token |
| `FORBIDDEN` | Insufficient permissions | Check account permissions |
| `INVALID_AUTH_TOKEN` | Auth token is invalid or expired | Refresh authentication token |

**Example:**
```json
{
  "success": false,
  "errorCode": "UNAUTHORIZED",
  "errorMessage": "Authentication is required to access this resource",
  "timestamp": "2026-02-02T09:30:00Z"
}
```

### Blockchain Errors (HTTP 422, 502)

| Error Code | Description | Resolution |
|------------|-------------|------------|
| `BLOCKCHAIN_CONNECTION_ERROR` | Cannot connect to blockchain network | Check network status, retry later |
| `TRANSACTION_FAILED` | Transaction failed on blockchain | Check account balance, transaction parameters |
| `CONTRACT_EXECUTION_FAILED` | Smart contract execution failed | Verify contract parameters |
| `INSUFFICIENT_FUNDS` | Account has insufficient funds | Add funds to account |
| `TRANSACTION_REJECTED` | Network rejected transaction | Check transaction validity |
| `GAS_ESTIMATION_FAILED` | Cannot estimate gas cost | Check network availability |

**Example:**
```json
{
  "success": false,
  "errorCode": "BLOCKCHAIN_CONNECTION_ERROR",
  "errorMessage": "Failed to connect to Algorand mainnet blockchain network. Please try again later.",
  "details": {
    "network": "mainnet",
    "endpoint": "https://mainnet-api.4160.nodely.dev"
  },
  "timestamp": "2026-02-02T09:30:00Z"
}
```

### External Service Errors (HTTP 502, 503)

| Error Code | Description | Resolution |
|------------|-------------|------------|
| `IPFS_SERVICE_ERROR` | IPFS service unavailable | Retry later |
| `EXTERNAL_SERVICE_ERROR` | External API failed | Check service status |
| `CIRCUIT_BREAKER_OPEN` | Service temporarily unavailable | Wait and retry |

**Example:**
```json
{
  "success": false,
  "errorCode": "IPFS_SERVICE_ERROR",
  "errorMessage": "Failed to upload metadata to IPFS. Please try again later.",
  "timestamp": "2026-02-02T09:30:00Z"
}
```

### Timeout Errors (HTTP 408)

| Error Code | Description | Resolution |
|------------|-------------|------------|
| `TIMEOUT` | Request timed out | Retry the operation |

**Example:**
```json
{
  "success": false,
  "errorCode": "TIMEOUT",
  "errorMessage": "The token deployment operation timed out. Please try again.",
  "timestamp": "2026-02-02T09:30:00Z"
}
```

### Rate Limiting Errors (HTTP 429)

| Error Code | Description | Resolution |
|------------|-------------|------------|
| `RATE_LIMIT_EXCEEDED` | Too many requests | Wait and retry with backoff |
| `SUBSCRIPTION_LIMIT_REACHED` | Subscription limits exceeded | Upgrade subscription tier |

### Server Errors (HTTP 500)

| Error Code | Description | Resolution |
|------------|-------------|------------|
| `INTERNAL_SERVER_ERROR` | Unexpected server error | Contact support with correlationId |
| `CONFIGURATION_ERROR` | Server configuration issue | Contact support |
| `UNEXPECTED_ERROR` | Unknown error occurred | Contact support with correlationId |

## Token Deployment Response Format

Token deployment endpoints return enhanced response objects that include error information:

```json
{
  "success": false,
  "errorMessage": "Deployment failed",
  "errorCode": "BLOCKCHAIN_CONNECTION_ERROR",
  "errorDetails": {
    "network": "mainnet"
  },
  "timestamp": "2026-02-02T09:30:00Z"
}
```

On success:
```json
{
  "success": true,
  "transactionHash": "0x123...",
  "contractAddress": "0xabc...",
  "timestamp": "2026-02-02T09:30:00Z"
}
```

## Retry Strategy

The API implements automatic retry with exponential backoff for transient failures:

- **Max Retry Attempts**: 3
- **Initial Delay**: 500ms
- **Backoff Type**: Exponential with jitter
- **Circuit Breaker**: Opens after 50% failure rate over 60 seconds
- **Circuit Breaker Duration**: 15 seconds
- **Total Request Timeout**: 60 seconds
- **Per-Attempt Timeout**: 20 seconds

### Client-Side Retry Recommendations

For client applications, implement retry logic for the following error codes:

**Retry Immediately (with exponential backoff):**
- `TIMEOUT`
- `CIRCUIT_BREAKER_OPEN`
- `BLOCKCHAIN_CONNECTION_ERROR`
- `EXTERNAL_SERVICE_ERROR`

**Do NOT Retry:**
- `INVALID_REQUEST`
- `UNAUTHORIZED`
- `FORBIDDEN`
- `MISSING_REQUIRED_FIELD`
- `INVALID_TOKEN_PARAMETERS`

## Best Practices

### 1. Check Error Codes First

Always check the `errorCode` field for programmatic error handling:

```typescript
if (!response.success) {
  switch (response.errorCode) {
    case 'BLOCKCHAIN_CONNECTION_ERROR':
      // Retry with exponential backoff
      break;
    case 'INVALID_REQUEST':
      // Fix request and retry
      break;
    case 'UNAUTHORIZED':
      // Refresh authentication
      break;
    default:
      // Handle unexpected errors
      break;
  }
}
```

### 2. Use Correlation IDs for Support

When contacting support, always include the `correlationId` from error responses for faster troubleshooting.

### 3. Implement Circuit Breakers

If you're making multiple requests, implement client-side circuit breakers to prevent cascading failures.

### 4. Monitor Error Rates

Track error rates by error code to identify systemic issues:
- High `BLOCKCHAIN_CONNECTION_ERROR`: Network issues
- High `TIMEOUT`: Performance issues
- High `INVALID_REQUEST`: Client integration issues

## Common Error Scenarios and Solutions

### Scenario 1: Token Deployment Fails

**Error:**
```json
{
  "errorCode": "BLOCKCHAIN_CONNECTION_ERROR",
  "errorMessage": "Failed to connect to Base blockchain network."
}
```

**Solution:**
1. Check network status at https://status.base.org
2. Verify RPC endpoint configuration
3. Retry after 30 seconds
4. If persists, try alternative RPC endpoint

### Scenario 2: IPFS Upload Fails

**Error:**
```json
{
  "errorCode": "IPFS_SERVICE_ERROR",
  "errorMessage": "Failed to upload metadata to IPFS."
}
```

**Solution:**
1. Verify IPFS service is running
2. Check file size is under 10MB limit
3. Retry upload
4. If persists, check IPFS credentials

### Scenario 3: Invalid Token Parameters

**Error:**
```json
{
  "errorCode": "INVALID_TOKEN_PARAMETERS",
  "errorMessage": "Token decimals must be between 0 and 18"
}
```

**Solution:**
1. Review API documentation for valid parameter ranges
2. Update request with valid parameters
3. Retry request

## Development vs Production

### Development Environment

In development, error responses include additional debugging information:

```json
{
  "success": false,
  "errorCode": "INTERNAL_SERVER_ERROR",
  "errorMessage": "An error occurred",
  "details": {
    "exceptionType": "NullReferenceException",
    "exceptionMessage": "Object reference not set",
    "stackTrace": "at BiatecTokensApi..."
  }
}
```

### Production Environment

In production, error responses exclude sensitive debugging information:

```json
{
  "success": false,
  "errorCode": "INTERNAL_SERVER_ERROR",
  "errorMessage": "An unexpected error occurred while processing your request"
}
```

## Contact Support

For unresolved errors, contact support with:
- **Correlation ID** from error response
- **Timestamp** of the error
- **Error code** and message
- **Request details** (endpoint, parameters)
- **Steps to reproduce** the error

Support Email: support@biatec.io

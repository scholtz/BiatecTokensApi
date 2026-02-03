# Security & Compliance Activity API

## Overview

The Security & Compliance Activity API provides enterprise-grade endpoints for audit trail tracking, security event monitoring, transaction history, and account recovery guidance. This API is designed to support MICA compliance requirements and enable transparent security monitoring for enterprise customers.

**Base Path:** `/api/v1/security`

**Authentication:** All endpoints require ARC-0014 Algorand authentication

## Endpoints

### 1. Get Security Activity Events

Retrieves paginated security activity events with comprehensive filtering options.

**Endpoint:** `GET /api/v1/security/activity`

**Query Parameters:**
- `accountId` (string, optional): Filter by account ID (defaults to authenticated user)
- `eventType` (SecurityEventType, optional): Filter by event type
- `severity` (EventSeverity, optional): Filter by severity level
- `fromDate` (DateTime, optional): Start date filter (ISO 8601 format)
- `toDate` (DateTime, optional): End date filter (ISO 8601 format)
- `success` (boolean, optional): Filter by operation result
- `page` (integer, optional): Page number (default: 1)
- `pageSize` (integer, optional): Page size (default: 50, max: 100)

**Security Event Types:**
- `Login` - User login event
- `Logout` - User logout event
- `LoginFailed` - Failed login attempt
- `PasswordReset` - Password reset requested
- `NetworkSwitch` - Network switch event
- `TokenDeployment` - Token deployment initiated
- `TokenDeploymentSuccess` - Token deployment succeeded
- `TokenDeploymentFailure` - Token deployment failed
- `SubscriptionChange` - Subscription plan changed
- `ComplianceCheck` - Compliance check performed
- `WhitelistOperation` - Whitelist operation
- `BlacklistOperation` - Blacklist operation
- `AuditExport` - Audit export requested
- `Recovery` - Recovery operation
- `AccountCreated` - Account created
- `AccountSuspended` - Account suspended
- `AccountActivated` - Account activated

**Event Severity Levels:**
- `Info` - Informational event
- `Warning` - Warning event
- `Error` - Error event
- `Critical` - Critical security event

**Response Schema:**
```json
{
  "success": true,
  "events": [
    {
      "eventId": "550e8400-e29b-41d4-a716-446655440000",
      "accountId": "ADDR...",
      "eventType": "Login",
      "severity": "Info",
      "timestamp": "2024-02-03T10:30:00Z",
      "summary": "User logged in successfully",
      "metadata": {},
      "correlationId": "req-12345",
      "sourceIp": "192.168.1.1",
      "userAgent": "Mozilla/5.0...",
      "success": true,
      "errorMessage": null
    }
  ],
  "totalCount": 150,
  "page": 1,
  "pageSize": 50,
  "totalPages": 3
}
```

**Example Request:**
```bash
curl -X GET "https://api.example.com/api/v1/security/activity?eventType=Login&page=1&pageSize=20" \
  -H "Authorization: SigTx <signed-transaction>"
```

**Use Cases:**
- Security monitoring and incident investigation
- Compliance reporting and audits
- User activity tracking
- Troubleshooting deployment failures

---

### 2. Get Transaction History

Retrieves token deployment transaction history with filtering by network, token standard, and status.

**Endpoint:** `GET /api/v1/security/transactions`

**Query Parameters:**
- `accountId` (string, optional): Filter by account ID (defaults to authenticated user)
- `network` (string, optional): Filter by network (e.g., voimain-v1.0, mainnet-v1.0)
- `tokenStandard` (string, optional): Filter by token standard (ASA, ARC3, ARC200, ERC20)
- `status` (string, optional): Filter by deployment status (success, failed, pending)
- `fromDate` (DateTime, optional): Start date filter (ISO 8601 format)
- `toDate` (DateTime, optional): End date filter (ISO 8601 format)
- `page` (integer, optional): Page number (default: 1)
- `pageSize` (integer, optional): Page size (default: 50, max: 100)

**Supported Networks:**
- `voimain-v1.0` - VOI mainnet
- `aramidmain-v1.0` - Aramid mainnet
- `mainnet-v1.0` - Algorand mainnet
- `testnet-v1.0` - Algorand testnet
- `base` - Base blockchain (EVM)

**Supported Token Standards:**
- `ASA` - Algorand Standard Assets
- `ARC3` - Algorand tokens with rich metadata
- `ARC200` - Advanced smart contract tokens
- `ERC20` - Ethereum-compatible tokens

**Response Schema:**
```json
{
  "success": true,
  "transactions": [
    {
      "transactionId": "TXN123...",
      "assetId": 123456,
      "network": "voimain-v1.0",
      "tokenStandard": "ARC200",
      "tokenName": "My Token",
      "tokenSymbol": "MTK",
      "status": "success",
      "deployedAt": "2024-02-03T10:30:00Z",
      "creatorAddress": "ADDR...",
      "confirmedRound": 12345678,
      "errorMessage": null
    }
  ],
  "totalCount": 25,
  "page": 1,
  "pageSize": 50,
  "totalPages": 1
}
```

**Example Request:**
```bash
curl -X GET "https://api.example.com/api/v1/security/transactions?network=voimain-v1.0&status=success" \
  -H "Authorization: SigTx <signed-transaction>"
```

**Use Cases:**
- Tracking deployment success rates
- Debugging failed deployments
- Audit trail for compliance
- Portfolio management

---

### 3. Export Audit Trail

Exports security activity events in CSV or JSON format with idempotency support.

**Endpoint:** `POST /api/v1/security/audit/export`

**Query Parameters:**
- `format` (string, required): Export format - `csv` or `json`
- `accountId` (string, optional): Filter by account ID (defaults to authenticated user)
- `eventType` (SecurityEventType, optional): Filter by event type
- `severity` (EventSeverity, optional): Filter by severity level
- `fromDate` (DateTime, optional): Start date filter (ISO 8601 format)
- `toDate` (DateTime, optional): End date filter (ISO 8601 format)

**Headers:**
- `Idempotency-Key` (optional): Prevents duplicate exports from retries (cached for 24 hours)

**Export Limits:**
- Maximum 10,000 records per export
- Quota based on subscription tier:
  - Free: 10 exports/month
  - Basic: 50 exports/month
  - Premium: Unlimited exports

**Response (Metadata):**
```json
{
  "success": true,
  "exportId": "exp-550e8400-e29b-41d4-a716-446655440000",
  "status": "completed",
  "format": "csv",
  "recordCount": 250,
  "idempotencyHit": false,
  "quota": {
    "maxExportsPerMonth": 50,
    "exportsUsed": 5,
    "exportsRemaining": 45,
    "maxRecordsPerExport": 10000
  }
}
```

**Response (File):**
- CSV: UTF-8 encoded with proper escaping
- JSON: Pretty-printed with metadata

**CSV Format:**
```csv
EventId,AccountId,EventType,Severity,Timestamp,Summary,Success,ErrorMessage,CorrelationId,SourceIp,UserAgent
550e8400-...,ADDR...,Login,Info,2024-02-03T10:30:00Z,User logged in,true,,,192.168.1.1,Mozilla/5.0...
```

**JSON Format:**
```json
{
  "exportedAt": "2024-02-03T11:00:00Z",
  "recordCount": 250,
  "events": [
    {
      "eventId": "550e8400-...",
      "accountId": "ADDR...",
      "eventType": "Login",
      "severity": "Info",
      "timestamp": "2024-02-03T10:30:00Z",
      "summary": "User logged in successfully",
      "success": true
    }
  ]
}
```

**Example Request:**
```bash
curl -X POST "https://api.example.com/api/v1/security/audit/export?format=csv" \
  -H "Authorization: SigTx <signed-transaction>" \
  -H "Idempotency-Key: unique-key-12345"
```

**Response Headers:**
- `X-Idempotency-Hit: true` - Indicates cached response
- `Content-Disposition: attachment; filename=audit-trail-20240203110000.csv`

**Use Cases:**
- MICA compliance reporting
- Internal audit reviews
- Integration with SIEM systems
- Long-term archival

---

### 4. Get Recovery Guidance

Retrieves account recovery guidance including eligibility status and step-by-step instructions.

**Endpoint:** `GET /api/v1/security/recovery`

**No Query Parameters**

**Recovery Eligibility States:**
- `Eligible` - Recovery is available
- `Cooldown` - Recovery was recently requested, must wait before retrying
- `AlreadySent` - Recovery email already sent
- `NotConfigured` - Recovery email not set up
- `AccountLocked` - Account is locked, contact support

**Response Schema:**
```json
{
  "success": true,
  "eligibility": "Eligible",
  "lastRecoveryAttempt": null,
  "cooldownRemaining": 0,
  "steps": [
    {
      "stepNumber": 1,
      "title": "Verify Identity",
      "instructions": "Verify your identity using your registered email or authentication method.",
      "completed": false
    },
    {
      "stepNumber": 2,
      "title": "Confirm Recovery Request",
      "instructions": "Confirm that you want to initiate account recovery. This will send a recovery link to your registered email.",
      "completed": false
    },
    {
      "stepNumber": 3,
      "title": "Check Recovery Email",
      "instructions": "Check your email inbox for the recovery link. The link will be valid for 24 hours.",
      "completed": false
    },
    {
      "stepNumber": 4,
      "title": "Reset Access",
      "instructions": "Follow the link in the email to reset your account access and set up new credentials.",
      "completed": false
    }
  ],
  "notes": "Recovery process typically takes 5-10 minutes. If you don't receive the email, check your spam folder or contact support."
}
```

**Example Request:**
```bash
curl -X GET "https://api.example.com/api/v1/security/recovery" \
  -H "Authorization: SigTx <signed-transaction>"
```

**Cooldown Policy:**
- 1 hour cooldown between recovery requests
- Maximum 3 recovery attempts per 24 hours
- Automatic account lock after repeated failed attempts

**Use Cases:**
- Account recovery UI guidance
- Support ticket automation
- Security center dashboard

---

## Error Responses

All endpoints return standardized error responses with error codes and remediation hints.

**Error Response Schema:**
```json
{
  "success": false,
  "errorCode": "ERROR_CODE",
  "errorMessage": "Human-readable error message",
  "remediationHint": "Suggestion on how to fix the error",
  "timestamp": "2024-02-03T10:30:00Z"
}
```

**Common Error Codes:**
- `INVALID_REQUEST` (400) - Invalid request parameters
- `UNAUTHORIZED` (401) - Authentication required
- `INVALID_EXPORT_FORMAT` (400) - Invalid export format (use csv or json)
- `EXPORT_QUOTA_EXCEEDED` (429) - Export quota exceeded for this month
- `RECOVERY_NOT_AVAILABLE` (400) - Recovery not available
- `RECOVERY_COOLDOWN_ACTIVE` (429) - Recovery cooldown active
- `INTERNAL_SERVER_ERROR` (500) - Internal server error

**Example Error Response:**
```json
{
  "success": false,
  "errorCode": "EXPORT_QUOTA_EXCEEDED",
  "errorMessage": "Export quota exceeded for this month",
  "remediationHint": "Upgrade your subscription plan to increase export limits.",
  "timestamp": "2024-02-03T10:30:00Z"
}
```

---

## Authentication

All endpoints require ARC-0014 Algorand authentication. Include the signed transaction in the Authorization header:

```
Authorization: SigTx <base64-encoded-signed-transaction>
```

For more information on ARC-0014 authentication, see the [Algorand Authentication documentation](https://github.com/algorandfoundation/ARCs/blob/main/ARCs/arc-0014.md).

---

## Rate Limiting

API requests are subject to rate limiting based on subscription tier:
- Free: 100 requests/hour
- Basic: 1,000 requests/hour
- Premium: 10,000 requests/hour

When rate limit is exceeded, the API returns a 429 status code with error code `RATE_LIMIT_EXCEEDED`.

---

## Pagination

Endpoints that return lists support pagination with the following parameters:
- `page` (default: 1) - Page number
- `pageSize` (default: 50, max: 100) - Number of items per page

Response includes pagination metadata:
- `totalCount` - Total number of items
- `page` - Current page number
- `pageSize` - Items per page
- `totalPages` - Total number of pages

---

## Best Practices

### Security Event Logging
- Always include correlation IDs for related events
- Use appropriate severity levels (Info, Warning, Error, Critical)
- Include contextual metadata where relevant
- Log both successful and failed operations

### Audit Trail Export
- Use idempotency keys to prevent duplicate exports from retries
- Export in batches if total records exceed 10,000
- Store exports securely with appropriate access controls
- Include date range filters to limit export size

### Recovery Operations
- Check eligibility status before initiating recovery
- Respect cooldown periods to avoid account lockout
- Provide clear instructions to users
- Monitor failed recovery attempts

### Performance Optimization
- Use appropriate page sizes (50-100 recommended)
- Apply filters to reduce result set size
- Cache frequently accessed data on client side
- Use date range filters for historical data queries

---

## Frontend Integration Guide

### Activity Timeline Component

```typescript
// Fetch security activity events
async function fetchActivity(filters: ActivityFilters) {
  const params = new URLSearchParams({
    page: filters.page.toString(),
    pageSize: filters.pageSize.toString(),
    ...(filters.eventType && { eventType: filters.eventType }),
    ...(filters.severity && { severity: filters.severity }),
    ...(filters.fromDate && { fromDate: filters.fromDate.toISOString() }),
  });

  const response = await fetch(`/api/v1/security/activity?${params}`, {
    headers: {
      'Authorization': `SigTx ${signedTransaction}`,
    },
  });

  return response.json();
}
```

### Transaction History Component

```typescript
// Fetch transaction history
async function fetchTransactions(network?: string) {
  const params = new URLSearchParams({
    page: '1',
    pageSize: '50',
    ...(network && { network }),
  });

  const response = await fetch(`/api/v1/security/transactions?${params}`, {
    headers: {
      'Authorization': `SigTx ${signedTransaction}`,
    },
  });

  return response.json();
}
```

### Export Audit Trail

```typescript
// Export audit trail with idempotency
async function exportAuditTrail(format: 'csv' | 'json') {
  const idempotencyKey = generateIdempotencyKey(); // e.g., UUID
  
  const response = await fetch(`/api/v1/security/audit/export?format=${format}`, {
    method: 'POST',
    headers: {
      'Authorization': `SigTx ${signedTransaction}`,
      'Idempotency-Key': idempotencyKey,
    },
  });

  if (response.ok) {
    const blob = await response.blob();
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `audit-trail-${Date.now()}.${format}`;
    a.click();
  }
}
```

### Recovery Guidance Component

```typescript
// Fetch recovery guidance
async function fetchRecoveryGuidance() {
  const response = await fetch('/api/v1/security/recovery', {
    headers: {
      'Authorization': `SigTx ${signedTransaction}`,
    },
  });

  return response.json();
}
```

---

## Support

For questions or issues with the Security & Compliance Activity API:
- Documentation: [API Reference](/swagger)
- Support: support@biatec.io
- GitHub: https://github.com/scholtz/BiatecTokensApi

---

## Version History

**v1.0.0** (2024-02-03)
- Initial release of Security & Compliance Activity API
- Security activity events endpoint
- Transaction history endpoint
- Audit trail export with CSV/JSON support
- Recovery guidance endpoint
- Idempotency support for exports
- Subscription-based quota management

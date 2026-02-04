# Deployment Status and Audit Trail Pipeline

## Overview

The BiatecTokensApi implements a comprehensive deployment status tracking and audit trail system designed to meet enterprise compliance requirements and provide deterministic, observable deployment status updates for token deployment across all supported networks.

## State Machine

The deployment pipeline follows a well-defined state machine with clear transitions:

```
Queued → Submitted → Pending → Confirmed → Indexed → Completed
  ↓         ↓          ↓          ↓          ↓         ↓
Failed ← ← ← ← ← ← ← ← ← ← ← ← ← ← ← ← ← ← (from any non-terminal state)
  ↓
Queued (retry allowed)

Queued → Cancelled (user-initiated)
```

### States

- **Queued**: Deployment request received and queued for processing
- **Submitted**: Transaction submitted to blockchain network
- **Pending**: Transaction awaiting confirmation
- **Confirmed**: Transaction confirmed by blockchain (included in block)
- **Indexed**: Transaction indexed by explorers and fully propagated
- **Completed**: Deployment completed successfully (terminal state)
- **Failed**: Deployment failed at any stage (can retry)
- **Cancelled**: User cancelled deployment before submission (terminal state)

### Valid Transitions

| From State | To States |
|-----------|-----------|
| Queued | Submitted, Failed, Cancelled |
| Submitted | Pending, Failed |
| Pending | Confirmed, Failed |
| Confirmed | Indexed, Completed, Failed |
| Indexed | Completed, Failed |
| Completed | _(none - terminal)_ |
| Failed | Queued _(retry)_ |
| Cancelled | _(none - terminal)_ |

## API Endpoints

### Get Deployment Status

```http
GET /api/v1/token/deployments/{deploymentId}
Authorization: SigTx <your-arc14-signed-transaction>
```

**Response:**
```json
{
  "success": true,
  "deployment": {
    "deploymentId": "550e8400-e29b-41d4-a716-446655440000",
    "currentStatus": "Completed",
    "tokenType": "ERC20_Mintable",
    "tokenName": "My Token",
    "tokenSymbol": "MTK",
    "network": "base-mainnet",
    "deployedBy": "0x742d35Cc6634C0532925a3b8D4434d3C7f2db9bc",
    "assetIdentifier": "0x123...",
    "transactionHash": "0xabc...",
    "createdAt": "2026-02-04T10:00:00Z",
    "updatedAt": "2026-02-04T10:05:00Z",
    "statusHistory": [
      {
        "id": "entry-1",
        "deploymentId": "550e8400-e29b-41d4-a716-446655440000",
        "status": "Queued",
        "timestamp": "2026-02-04T10:00:00Z",
        "message": "Deployment request queued for processing"
      },
      {
        "id": "entry-2",
        "deploymentId": "550e8400-e29b-41d4-a716-446655440000",
        "status": "Submitted",
        "timestamp": "2026-02-04T10:01:00Z",
        "message": "Transaction submitted",
        "transactionHash": "0xabc..."
      },
      {
        "id": "entry-3",
        "deploymentId": "550e8400-e29b-41d4-a716-446655440000",
        "status": "Confirmed",
        "timestamp": "2026-02-04T10:03:00Z",
        "message": "Transaction confirmed",
        "confirmedRound": 12345
      },
      {
        "id": "entry-4",
        "deploymentId": "550e8400-e29b-41d4-a716-446655440000",
        "status": "Completed",
        "timestamp": "2026-02-04T10:05:00Z",
        "message": "Deployment completed successfully"
      }
    ]
  }
}
```

### List Deployments

```http
GET /api/v1/token/deployments?page=1&pageSize=50&status=Completed&network=base-mainnet
Authorization: SigTx <your-arc14-signed-transaction>
```

**Query Parameters:**
- `deployedBy` (optional): Filter by deployer address
- `network` (optional): Filter by network
- `tokenType` (optional): Filter by token type
- `status` (optional): Filter by current status
- `fromDate` (optional): Start date (ISO 8601)
- `toDate` (optional): End date (ISO 8601)
- `page` (default: 1): Page number
- `pageSize` (default: 50, max: 100): Results per page

**Response:**
```json
{
  "success": true,
  "deployments": [...],
  "totalCount": 250,
  "page": 1,
  "pageSize": 50,
  "totalPages": 5
}
```

### Get Deployment History

```http
GET /api/v1/token/deployments/{deploymentId}/history
Authorization: SigTx <your-arc14-signed-transaction>
```

Returns the complete status history for a deployment in chronological order.

### Cancel Deployment

```http
POST /api/v1/token/deployments/{deploymentId}/cancel
Authorization: SigTx <your-arc14-signed-transaction>
Content-Type: application/json

{
  "reason": "User changed configuration"
}
```

**Response:**
```json
{
  "success": true,
  "message": "Deployment cancelled successfully"
}
```

**Note:** Can only cancel deployments in Queued status. Once submitted to blockchain, deployments cannot be cancelled.

### Export Audit Trail

```http
GET /api/v1/token/deployments/{deploymentId}/audit-trail?format=json
Authorization: SigTx <your-arc14-signed-transaction>
```

**Formats:**
- `format=json` - Returns JSON file with complete audit trail
- `format=csv` - Returns CSV file suitable for spreadsheet import

**JSON Response:**
```json
{
  "deploymentId": "550e8400-e29b-41d4-a716-446655440000",
  "tokenType": "ERC20_Mintable",
  "tokenName": "My Token",
  "tokenSymbol": "MTK",
  "network": "base-mainnet",
  "deployedBy": "0x742d35Cc6634C0532925a3b8D4434d3C7f2db9bc",
  "assetIdentifier": "0x123...",
  "transactionHash": "0xabc...",
  "currentStatus": "Completed",
  "createdAt": "2026-02-04T10:00:00Z",
  "updatedAt": "2026-02-04T10:05:00Z",
  "statusHistory": [...],
  "complianceSummary": "No compliance checks performed",
  "totalDurationMs": 300000,
  "errorSummary": null
}
```

### Bulk Export Audit Trails

```http
POST /api/v1/token/deployments/audit-trail/export
Authorization: SigTx <your-arc14-signed-transaction>
X-Idempotency-Key: unique-key-123 (optional)
Content-Type: application/json

{
  "format": "json",
  "network": "base-mainnet",
  "fromDate": "2026-02-01T00:00:00Z",
  "toDate": "2026-02-04T23:59:59Z",
  "page": 1,
  "pageSize": 100
}
```

**Request Fields:**
- `format`: "json" or "csv"
- `deployedBy` (optional): Filter by deployer
- `network` (optional): Filter by network
- `tokenType` (optional): Filter by token type
- `status` (optional): Filter by status
- `fromDate` (optional): Start date
- `toDate` (optional): End date
- `page` (default: 1): Page number
- `pageSize` (default: 100, max: 1000): Records per page

**Response:**
```json
{
  "success": true,
  "data": "[...JSON array or CSV string...]",
  "format": "json",
  "recordCount": 42,
  "isCached": false,
  "generatedAt": "2026-02-04T10:00:00Z"
}
```

**Idempotency:**
- Include `X-Idempotency-Key` header for large exports
- Results cached for 1 hour
- Repeated requests with same key return cached results
- Key must be unique per request parameters

### Get Deployment Metrics

```http
GET /api/v1/token/deployments/metrics?fromDate=2026-02-03T00:00:00Z&network=base-mainnet
Authorization: SigTx <your-arc14-signed-transaction>
```

**Query Parameters:**
- `fromDate` (optional): Start date (default: 24 hours ago)
- `toDate` (optional): End date (default: now)
- `network` (optional): Filter by network
- `tokenType` (optional): Filter by token type
- `deployedBy` (optional): Filter by deployer

**Response:**
```json
{
  "success": true,
  "metrics": {
    "totalDeployments": 150,
    "successfulDeployments": 142,
    "failedDeployments": 5,
    "pendingDeployments": 2,
    "cancelledDeployments": 1,
    "successRate": 94.67,
    "failureRate": 3.33,
    "averageDurationMs": 285000,
    "medianDurationMs": 270000,
    "p95DurationMs": 450000,
    "fastestDurationMs": 180000,
    "slowestDurationMs": 600000,
    "failuresByCategory": {
      "NetworkError": 3,
      "ValidationError": 1,
      "InsufficientFunds": 1
    },
    "deploymentsByNetwork": {
      "base-mainnet": 100,
      "voimain-v1.0": 50
    },
    "deploymentsByTokenType": {
      "ERC20_Mintable": 80,
      "ARC200_Mintable": 70
    },
    "averageDurationByTransition": {
      "Queued->Submitted": 45000,
      "Submitted->Pending": 10000,
      "Pending->Confirmed": 180000,
      "Confirmed->Completed": 50000
    },
    "retriedDeployments": 3,
    "periodStart": "2026-02-03T00:00:00Z",
    "periodEnd": "2026-02-04T10:00:00Z",
    "calculatedAt": "2026-02-04T10:00:00Z"
  }
}
```

## Error Handling

### Error Categories

Errors are categorized to provide actionable feedback:

1. **NetworkError**: Connectivity or RPC issues (retryable, 30s delay)
2. **ValidationError**: Invalid parameters (not retryable)
3. **ComplianceError**: Regulatory violations (not retryable)
4. **UserRejection**: User cancelled (retryable)
5. **InsufficientFunds**: Low balance (retryable after funding)
6. **TransactionFailure**: Blockchain rejection (retryable, 60s delay)
7. **ConfigurationError**: System misconfiguration (not retryable)
8. **RateLimitExceeded**: Too many requests (retryable after cooldown)
9. **InternalError**: System error (retryable, 120s delay)

### Error Response

```json
{
  "success": false,
  "deployment": {
    "deploymentId": "...",
    "currentStatus": "Failed",
    "errorMessage": "Transaction failed on the blockchain",
    "statusHistory": [
      {
        "status": "Failed",
        "errorDetails": {
          "category": "TransactionFailure",
          "errorCode": "TRANSACTION_FAILED",
          "technicalMessage": "Contract execution reverted: Insufficient allowance",
          "userMessage": "The transaction failed on the blockchain. Please check the transaction details and try again.",
          "isRetryable": true,
          "suggestedRetryDelaySeconds": 60,
          "timestamp": "2026-02-04T10:05:00Z"
        }
      }
    ]
  }
}
```

## Integration Guide

### Frontend Integration

1. **Poll for status updates**:
```javascript
async function pollDeploymentStatus(deploymentId) {
  while (true) {
    const response = await fetch(`/api/v1/token/deployments/${deploymentId}`, {
      headers: {
        'Authorization': `SigTx ${signedTransaction}`
      }
    });
    
    const data = await response.json();
    
    // Update UI
    updateStatusUI(data.deployment.currentStatus);
    
    // Check if terminal state
    if (['Completed', 'Failed', 'Cancelled'].includes(data.deployment.currentStatus)) {
      break;
    }
    
    // Wait before next poll
    await new Promise(resolve => setTimeout(resolve, 3000));
  }
}
```

2. **Display progress**:
```javascript
const statusLabels = {
  'Queued': 'Queued for processing...',
  'Submitted': 'Submitting transaction...',
  'Pending': 'Awaiting confirmation...',
  'Confirmed': 'Transaction confirmed!',
  'Indexed': 'Indexing on blockchain...',
  'Completed': 'Deployment complete!',
  'Failed': 'Deployment failed',
  'Cancelled': 'Deployment cancelled'
};

function updateStatusUI(status) {
  document.getElementById('status').textContent = statusLabels[status];
}
```

3. **Handle errors**:
```javascript
if (data.deployment.currentStatus === 'Failed') {
  const errorEntry = data.deployment.statusHistory.find(e => e.status === 'Failed');
  const errorDetails = errorEntry?.errorDetails;
  
  if (errorDetails) {
    // Show user-friendly message
    showError(errorDetails.userMessage);
    
    // Show retry button if retryable
    if (errorDetails.isRetryable) {
      showRetryButton(errorDetails.suggestedRetryDelaySeconds);
    }
  }
}
```

### Webhook Integration

Status changes trigger webhook events:
- `TokenDeploymentStarted` - When deployment is queued or submitted
- `TokenDeploymentConfirming` - When pending or confirmed
- `TokenDeploymentCompleted` - When deployment completes
- `TokenDeploymentFailed` - When deployment fails

See [WEBHOOKS.md](WEBHOOKS.md) for webhook configuration.

## Compliance Features

### Audit Trail Requirements

The audit trail system meets the following compliance requirements:

1. **Immutability**: Status history is append-only
2. **Traceability**: Every action includes actor and timestamp
3. **Completeness**: Full transaction lifecycle recorded
4. **Exportability**: JSON and CSV formats for regulatory review
5. **Retention**: Data retained as long as deployments exist
6. **Accessibility**: API access for compliance officers

### Compliance Checks

Compliance checks can be attached to status entries:

```json
{
  "status": "Confirmed",
  "complianceChecks": [
    {
      "checkName": "KYC_VERIFICATION",
      "passed": true,
      "message": "KYC verification completed",
      "checkedAt": "2026-02-04T10:00:00Z"
    },
    {
      "checkName": "WHITELIST_CHECK",
      "passed": true,
      "message": "Address verified on whitelist",
      "checkedAt": "2026-02-04T10:00:00Z",
      "context": {
        "whitelistId": "550e8400-e29b-41d4-a716-446655440000"
      }
    }
  ]
}
```

## Monitoring and Observability

### Metrics for Monitoring

Use the `/metrics` endpoint to build monitoring dashboards:

**Key Metrics:**
- **Success Rate**: Target >95%
- **Average Duration**: Target <5 minutes
- **P95 Duration**: Target <10 minutes
- **Failure Rate by Category**: Identify systemic issues
- **Pending Deployments**: Monitor for stuck transactions

### Alerting

Recommended alerts:
- Success rate drops below 90%
- P95 duration exceeds 15 minutes
- More than 10 pending deployments
- Network error rate exceeds 5%
- Any deployment stuck in Pending for >1 hour

### Structured Logging

Key events are logged with structured data:

```
INFO: Created deployment: DeploymentId={id}, TokenType={type}, Network={network}
INFO: Updated deployment status: DeploymentId={id}, Status={status}, Message={msg}
WARN: Invalid status transition: DeploymentId={id}, CurrentStatus={current}, NewStatus={new}
ERROR: Error updating deployment status: DeploymentId={id}, NewStatus={status}
```

## Best Practices

### For Backend Integration

1. **Always use deployment tracking** when creating tokens
2. **Update status immediately** after blockchain operations
3. **Include error details** when marking as failed
4. **Set appropriate metadata** for debugging
5. **Test state transitions** in unit tests

### For Frontend Integration

1. **Poll with exponential backoff** to reduce server load
2. **Show clear progress indicators** for each state
3. **Display user-friendly error messages** from errorDetails.userMessage
4. **Enable retry** only when errorDetails.isRetryable is true
5. **Respect suggestedRetryDelaySeconds** before allowing retry

### For Operations

1. **Monitor metrics endpoint** for SLA tracking
2. **Export audit trails regularly** for compliance archives
3. **Set up alerts** on key metrics
4. **Review failure categories** weekly to identify patterns
5. **Archive old deployments** according to retention policy

## Troubleshooting

### Deployment Stuck in Pending

1. Check transaction hash on blockchain explorer
2. Verify network is operational
3. Check if transaction was replaced/dropped
4. Review network gas prices and transaction fee

### High Failure Rate

1. Check metrics endpoint for failure breakdown by category
2. Review recent failed deployments
3. Export audit trails for detailed analysis
4. Check network health and RPC availability

### Incorrect Status

1. Verify state machine transitions are valid
2. Check status history for duplicate entries (idempotency issue)
3. Review logs for error messages
4. Verify timestamp ordering in status history

## Security Considerations

1. **Authentication**: All endpoints require ARC-0014 authentication
2. **Authorization**: Users can only access their own deployments
3. **Rate Limiting**: Protect against abuse
4. **Input Validation**: All parameters are validated
5. **Error Messages**: No sensitive data in user-facing errors
6. **Audit Logging**: All actions are logged for security review

## Support

For additional support:
- API Documentation: `/swagger` endpoint
- GitHub Issues: https://github.com/scholtz/BiatecTokensApi/issues
- Email: support@biatec.io

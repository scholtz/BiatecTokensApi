# Compliance Webhooks Documentation

## Overview

The BiatecTokensApi provides enterprise-grade compliance webhooks that emit events for whitelist changes, transfer denials, and audit trail operations. Webhooks enable real-time integration with external compliance systems, enabling automated workflows and comprehensive audit tracking.

## Features

- **Event-Driven Architecture**: Receive real-time notifications for compliance events
- **Signing Secrets**: All webhook payloads are signed with HMAC-SHA256 for verification
- **Exponential Backoff Retry**: Failed deliveries are retried with 1min, 5min, and 15min delays
- **Dead-Letter Tracking**: Failed deliveries are persisted for admin review
- **Event Filtering**: Subscribe to specific event types and filter by asset ID or network
- **Delivery History**: Comprehensive audit trail of all webhook deliveries

## Supported Event Types

### WhitelistAdd
Triggered when an address is added to the whitelist.

**Event Data:**
```json
{
  "eventType": "WhitelistAdd",
  "assetId": 12345,
  "network": "voimain-v1.0",
  "actor": "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA",
  "affectedAddress": "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
  "timestamp": "2026-01-24T12:00:00Z",
  "data": {
    "status": "Active",
    "role": "Investor",
    "kycVerified": true
  }
}
```

### WhitelistRemove
Triggered when an address is removed from the whitelist.

**Event Data:**
```json
{
  "eventType": "WhitelistRemove",
  "assetId": 12345,
  "network": "voimain-v1.0",
  "actor": "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA",
  "affectedAddress": "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
  "timestamp": "2026-01-24T12:00:00Z",
  "data": {
    "previousStatus": "Active"
  }
}
```

### TransferDeny
Triggered when a transfer is denied by whitelist rules.

**Event Data:**
```json
{
  "eventType": "TransferDeny",
  "assetId": 12345,
  "network": null,
  "actor": "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA",
  "affectedAddress": "SENDER_ADDRESS",
  "timestamp": "2026-01-24T12:00:00Z",
  "data": {
    "fromAddress": "SENDER_ADDRESS",
    "toAddress": "RECEIVER_ADDRESS",
    "amount": "1000",
    "denialReason": "Receiver address not whitelisted",
    "senderWhitelisted": true,
    "receiverWhitelisted": false
  }
}
```

### AuditExportCreated
Triggered when an audit log export is created.

**Event Data:**
```json
{
  "eventType": "AuditExportCreated",
  "assetId": 12345,
  "network": "voimain-v1.0",
  "actor": "SYSTEM",
  "timestamp": "2026-01-24T12:00:00Z",
  "data": {
    "format": "CSV",
    "recordCount": 1500,
    "category": "Whitelist",
    "fromDate": "2026-01-01T00:00:00Z",
    "toDate": "2026-01-24T12:00:00Z"
  }
}
```

## API Endpoints

### Create Webhook Subscription

**POST** `/api/v1/webhooks/subscriptions`

Creates a new webhook subscription.

**Request:**
```json
{
  "url": "https://your-server.com/webhook",
  "eventTypes": ["WhitelistAdd", "WhitelistRemove", "TransferDeny"],
  "description": "Production compliance webhook",
  "assetIdFilter": 12345,
  "networkFilter": "voimain-v1.0"
}
```

**Response:**
```json
{
  "success": true,
  "subscription": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "url": "https://your-server.com/webhook",
    "signingSecret": "dGVzdHNlY3JldA==",
    "eventTypes": ["WhitelistAdd", "WhitelistRemove", "TransferDeny"],
    "isActive": true,
    "createdBy": "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA",
    "createdAt": "2026-01-24T12:00:00Z"
  }
}
```

**Important:** Save the `signingSecret` - it's only provided once during creation!

### List Webhook Subscriptions

**GET** `/api/v1/webhooks/subscriptions`

Lists all webhook subscriptions for the authenticated user.

**Response:**
```json
{
  "success": true,
  "subscriptions": [...],
  "totalCount": 3
}
```

### Get Webhook Subscription

**GET** `/api/v1/webhooks/subscriptions/{subscriptionId}`

Retrieves details of a specific webhook subscription.

### Update Webhook Subscription

**PUT** `/api/v1/webhooks/subscriptions`

Updates an existing webhook subscription.

**Request:**
```json
{
  "subscriptionId": "550e8400-e29b-41d4-a716-446655440000",
  "isActive": false,
  "description": "Temporarily disabled",
  "eventTypes": ["WhitelistAdd"]
}
```

### Delete Webhook Subscription

**DELETE** `/api/v1/webhooks/subscriptions/{subscriptionId}`

Deletes a webhook subscription.

### Get Delivery History

**GET** `/api/v1/webhooks/deliveries`

Retrieves webhook delivery history with comprehensive filtering.

**Query Parameters:**
- `subscriptionId` (optional) - Filter by subscription ID
- `eventId` (optional) - Filter by event ID
- `success` (optional) - Filter by delivery success status
- `fromDate` (optional) - Start date filter (ISO 8601)
- `toDate` (optional) - End date filter (ISO 8601)
- `page` (optional) - Page number (default: 1)
- `pageSize` (optional) - Page size (default: 50, max: 100)

**Response:**
```json
{
  "success": true,
  "deliveries": [
    {
      "id": "delivery-id",
      "subscriptionId": "subscription-id",
      "eventId": "event-id",
      "success": true,
      "statusCode": 200,
      "attemptedAt": "2026-01-24T12:00:00Z",
      "retryCount": 0
    }
  ],
  "totalCount": 150,
  "successCount": 145,
  "failedCount": 5,
  "pendingRetries": 2
}
```

## Webhook Signature Verification

All webhook payloads include an `X-Webhook-Signature` header that should be verified to ensure the request is authentic.

### Verification Steps

1. Get the signing secret from your subscription
2. Compute HMAC-SHA256 of the raw request body using the signing secret
3. Base64-encode the result
4. Compare with the `X-Webhook-Signature` header

### Example Verification (Node.js)

```javascript
const crypto = require('crypto');

function verifyWebhook(body, signature, secret) {
  const hmac = crypto.createHmac('sha256', secret);
  hmac.update(body);
  const expectedSignature = hmac.digest('base64');
  
  return crypto.timingSafeEqual(
    Buffer.from(signature),
    Buffer.from(expectedSignature)
  );
}

// Express.js middleware
app.post('/webhook', express.raw({ type: 'application/json' }), (req, res) => {
  const signature = req.headers['x-webhook-signature'];
  const secret = process.env.WEBHOOK_SECRET;
  
  if (!verifyWebhook(req.body, signature, secret)) {
    return res.status(401).send('Invalid signature');
  }
  
  const event = JSON.parse(req.body);
  // Process event
  
  res.status(200).send('OK');
});
```

### Example Verification (C#)

```csharp
using System.Security.Cryptography;
using System.Text;

public bool VerifyWebhook(string body, string signature, string secret)
{
    using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
    var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
    var expectedSignature = Convert.ToBase64String(hashBytes);
    
    return signature == expectedSignature;
}
```

### Example Verification (Python)

```python
import hmac
import hashlib
import base64

def verify_webhook(body: str, signature: str, secret: str) -> bool:
    expected_signature = base64.b64encode(
        hmac.new(
            secret.encode('utf-8'),
            body.encode('utf-8'),
            hashlib.sha256
        ).digest()
    ).decode('utf-8')
    
    return hmac.compare_digest(signature, expected_signature)
```

## Retry Logic

Failed webhook deliveries are automatically retried with exponential backoff:

1. **First retry**: 1 minute after initial failure
2. **Second retry**: 5 minutes after first retry
3. **Third retry**: 15 minutes after second retry

After 3 failed attempts, the delivery enters dead-letter status and can be reviewed via the delivery history endpoint.

### Retry Conditions

Deliveries are retried on:
- HTTP 5xx server errors
- HTTP 429 Too Many Requests
- Network timeouts (30 seconds)

Deliveries are NOT retried on:
- HTTP 4xx client errors (except 429)
- HTTP 2xx success responses
- After 3 retry attempts

## Best Practices

### Endpoint Implementation

1. **Return 2xx quickly**: Process events asynchronously and return success immediately
2. **Verify signatures**: Always validate the `X-Webhook-Signature` header
3. **Handle idempotency**: Use the event ID to detect and ignore duplicate deliveries
4. **Log thoroughly**: Keep comprehensive logs for debugging

### Security

1. **Keep secrets secure**: Store signing secrets in secure configuration
2. **Use HTTPS**: Always use HTTPS endpoints for webhook URLs
3. **Validate payloads**: Verify event structure matches expected schema
4. **Rate limiting**: Implement rate limiting on your webhook endpoint

### Monitoring

1. **Check delivery history**: Regularly review failed deliveries
2. **Set up alerts**: Alert on sustained delivery failures
3. **Test subscriptions**: Periodically test webhook endpoints
4. **Update URLs**: Keep webhook URLs current and valid

## Integration Examples

### Real-Time Compliance Dashboard

```javascript
// Receive webhook events and update dashboard
app.post('/webhook', async (req, res) => {
  const event = req.body;
  
  switch (event.eventType) {
    case 'WhitelistAdd':
      await dashboard.updateWhitelistCount(event.assetId, +1);
      await dashboard.logActivity(event);
      break;
      
    case 'TransferDeny':
      await dashboard.incrementDeniedTransfers(event.assetId);
      await dashboard.sendAlert(event);
      break;
      
    case 'AuditExportCreated':
      await dashboard.updateLastExportTime(event.assetId);
      break;
  }
  
  res.status(200).send('OK');
});
```

### Automated Compliance Reporting

```python
@app.route('/webhook', methods=['POST'])
def webhook_handler():
    event = request.json
    
    if event['eventType'] == 'AuditExportCreated':
        # Automatically archive audit exports
        archive_audit_export(
            asset_id=event['assetId'],
            record_count=event['data']['recordCount'],
            timestamp=event['timestamp']
        )
        
        # Send notification to compliance team
        notify_compliance_team(event)
    
    return '', 200
```

## Troubleshooting

### Webhook Not Receiving Events

1. Verify subscription is active: `GET /api/v1/webhooks/subscriptions/{id}`
2. Check event type filters match the events being emitted
3. Verify asset ID and network filters (if set)
4. Check delivery history for errors: `GET /api/v1/webhooks/deliveries?subscriptionId={id}`

### Signature Verification Failing

1. Ensure you're using the raw request body (not parsed JSON)
2. Verify the signing secret is correct
3. Check for encoding issues (UTF-8)
4. Validate HMAC-SHA256 and Base64 encoding

### High Delivery Failure Rate

1. Check endpoint is accessible from internet
2. Verify endpoint returns 2xx within 30 seconds
3. Review server logs for errors
4. Ensure HTTPS certificate is valid
5. Check for rate limiting on your server

## Support

For additional support or questions:
- Review the API documentation at `/swagger`
- Check delivery history for error messages
- Contact support with subscription ID and event IDs

## Changelog

### v1.0.0 (2026-01-24)
- Initial release of compliance webhooks
- Support for WhitelistAdd, WhitelistRemove, TransferDeny, and AuditExportCreated events
- HMAC-SHA256 signature verification
- Exponential backoff retry with dead-letter tracking
- Comprehensive delivery history and monitoring

# Regulatory Webhooks and Compliance Event Subscriptions

## Overview

BiatecTokensApi provides a compliance-grade webhook system that delivers structured regulatory lifecycle events to external systems in real time. This enables enterprise customers to connect Biatec compliance decisions into downstream governance tools, case management systems, CRM workflows, audit aggregators, and approval engines without manual polling or export handling.

The webhook system is designed for regulated workflows. Delivery is authenticated, signed, auditable, and fail-closed. Persistent delivery failures are visible to operators and do not silently disappear.

---

## Event Taxonomy

Each compliance event belongs to one of the following lifecycle categories:

| Event Type | Description | Regulatory Relevance |
|---|---|---|
| `KycStatusChange` | KYC verification status changed for an asset or issuer | Triggered on approval, rejection, review-required, evidence expiry |
| `AmlStatusChange` | AML screening outcome changed | Triggered when AML screening provider returns a result or re-screens |
| `ComplianceBadgeUpdate` | Overall compliance status or badge updated | Triggered when token compliance posture changes (jurisdiction, framework) |
| `WhitelistAdd` | Address added to the whitelist | Triggered by whitelist policy enforcement |
| `WhitelistRemove` | Address removed from the whitelist | Triggered when whitelist entry expires or is revoked |
| `TransferDeny` | A token transfer was denied by whitelist rules | Triggered on each blocked transfer attempt |
| `AuditExportCreated` | An audit export record was generated | Triggered when compliance evidence is exported or requested |
| `TokenDeploymentStarted` | Token deployment initiated | Triggered at the start of a token deployment request |
| `TokenDeploymentConfirming` | Token deployment transaction is being confirmed on-chain | Triggered when blockchain confirmation is in progress |
| `TokenDeploymentCompleted` | Token deployment completed successfully | Triggered after successful blockchain confirmation |
| `TokenDeploymentFailed` | Token deployment failed | Triggered when deployment fails permanently |

---

## Subscription Management

### Create a Subscription

```http
POST /api/v1/webhooks/subscriptions
Authorization: Bearer <jwt>
Content-Type: application/json

{
  "url": "https://your-system.example.com/compliance-events",
  "eventTypes": ["KycStatusChange", "AmlStatusChange", "ComplianceBadgeUpdate"],
  "description": "Enterprise compliance integration endpoint",
  "assetIdFilter": 12345,
  "networkFilter": "mainnet"
}
```

**Response:**

```json
{
  "success": true,
  "subscription": {
    "id": "c4b3e2a1-...",
    "url": "https://your-system.example.com/compliance-events",
    "signingSecret": "base64-encoded-32-byte-secret",
    "eventTypes": ["KycStatusChange", "AmlStatusChange", "ComplianceBadgeUpdate"],
    "isActive": true,
    "createdBy": "USER_ADDRESS",
    "createdAt": "2026-03-15T02:00:00Z",
    "description": "Enterprise compliance integration endpoint",
    "assetIdFilter": 12345,
    "networkFilter": "mainnet"
  }
}
```

> **Important**: Store the `signingSecret` securely. It is used to verify that webhook deliveries originate from Biatec. The secret is generated server-side and is available in all owner-facing read operations (create, get, list). Rotate it by deleting and recreating the subscription.

### Get a Subscription

```http
GET /api/v1/webhooks/subscriptions/{subscriptionId}
Authorization: Bearer <jwt>
```

### List Subscriptions

```http
GET /api/v1/webhooks/subscriptions
Authorization: Bearer <jwt>
```

Returns all subscriptions owned by the authenticated user.

### Update a Subscription

```http
PUT /api/v1/webhooks/subscriptions
Authorization: Bearer <jwt>
Content-Type: application/json

{
  "subscriptionId": "c4b3e2a1-...",
  "isActive": false,
  "eventTypes": ["KycStatusChange"],
  "description": "Updated description"
}
```

Use `isActive: false` to pause delivery without deleting the subscription.

### Delete a Subscription

```http
DELETE /api/v1/webhooks/subscriptions/{subscriptionId}
Authorization: Bearer <jwt>
```

---

## Payload Schema

All webhook deliveries use the following stable JSON schema:

```json
{
  "Id": "evt_abc123",
  "EventType": "KycStatusChange",
  "Timestamp": "2026-03-15T02:00:00Z",
  "AssetId": 12345,
  "Network": "mainnet",
  "Actor": "ACTOR_ADDRESS",
  "AffectedAddress": "AFFECTED_ADDRESS",
  "Data": {
    "oldStatus": "Pending",
    "newStatus": "Approved",
    "kycProvider": "StripeIdentity",
    "verificationDate": "2026-03-15T02:00:00Z",
    "issuerName": "Example Issuer"
  }
}
```

### Field Reference

| Field | Type | Description |
|---|---|---|
| `Id` | string | Unique event identifier (UUID) |
| `EventType` | string | One of the event types in the taxonomy above |
| `Timestamp` | ISO 8601 UTC | When the event occurred |
| `AssetId` | number or null | Algorand ASA ID or EVM token ID (null for non-token events) |
| `Network` | string or null | Network identifier (e.g. "mainnet", "testnet") |
| `Actor` | string | Address or identity that triggered the event |
| `AffectedAddress` | string or null | Address affected by the event (e.g. for whitelist changes) |
| `Data` | object or null | Event-specific key-value pairs (see per-event examples below) |

### Event-Specific Data Fields

**KycStatusChange:**
```json
{
  "oldStatus": "Pending",
  "newStatus": "Approved",
  "kycProvider": "StripeIdentity",
  "verificationDate": "2026-03-15T02:00:00Z",
  "issuerName": "Example Issuer"
}
```

**AmlStatusChange:**
```json
{
  "verificationStatus": "Approved",
  "provider": "ComplyAdvantage",
  "verificationDate": "2026-03-15T02:00:00Z",
  "issuerName": "Example Issuer",
  "jurisdiction": "EU"
}
```

**ComplianceBadgeUpdate:**
```json
{
  "oldStatus": "Pending",
  "newStatus": "Compliant",
  "jurisdiction": "EU",
  "regulatoryFramework": "MiCA",
  "lastReview": "2026-03-15T02:00:00Z",
  "nextReview": "2026-09-15T02:00:00Z",
  "requiresAccreditedInvestors": false
}
```

---

## Authentication and Signature Verification

Each webhook delivery includes an `X-Webhook-Signature` header containing an HMAC-SHA256 signature of the raw payload body using the subscription's signing secret as the key.

### Signature Format

```
X-Webhook-Signature: <base64-encoded HMAC-SHA256>
X-Webhook-Event-Id: <event UUID>
X-Webhook-Event-Type: <event type string>
```

### Verifying the Signature (C# Example)

```csharp
using System.Security.Cryptography;
using System.Text;

bool VerifyWebhookSignature(string rawBody, string signingSecret, string signatureHeader)
{
    var keyBytes = Encoding.UTF8.GetBytes(signingSecret);
    var messageBytes = Encoding.UTF8.GetBytes(rawBody);

    using var hmac = new HMACSHA256(keyBytes);
    var hash = hmac.ComputeHash(messageBytes);
    var expected = Convert.ToBase64String(hash);

    // Use CryptographicOperations.FixedTimeEquals for timing-safe comparison.
    // A short-circuit string comparison (==) leaks timing information that an
    // attacker can exploit to forge signatures on compliance lifecycle events.
    var expectedBytes = Encoding.UTF8.GetBytes(expected);
    var actualBytes = Encoding.UTF8.GetBytes(signatureHeader);
    if (expectedBytes.Length != actualBytes.Length)
        return false;
    return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
}
```

### Verifying the Signature (Python Example)

```python
import hmac, hashlib, base64

def verify_webhook_signature(raw_body: bytes, signing_secret: str, signature_header: str) -> bool:
    key = signing_secret.encode("utf-8")
    digest = hmac.new(key, raw_body, hashlib.sha256).digest()
    expected = base64.b64encode(digest).decode("utf-8")
    return hmac.compare_digest(expected, signature_header)
```

### Verifying the Signature (Node.js Example)

```javascript
const crypto = require("crypto");

function verifyWebhookSignature(rawBody, signingSecret, signatureHeader) {
  const hmac = crypto.createHmac("sha256", signingSecret);
  hmac.update(rawBody, "utf8");
  const expected = hmac.digest("base64");
  return crypto.timingSafeEqual(
    Buffer.from(expected),
    Buffer.from(signatureHeader)
  );
}
```

> **Security note**: Always use a timing-safe comparison to prevent timing attacks.

---

## Retry and Failure Semantics

The webhook system uses an exponential backoff retry policy:

| Attempt | Delay | Total elapsed |
|---|---|---|
| Initial delivery | immediate | 0 |
| Retry 1 | 1 minute | ~1 min |
| Retry 2 | 5 minutes | ~6 min |
| Retry 3 | 15 minutes | ~21 min |

### Retry Eligibility

| HTTP Status | Retried? | Reason |
|---|---|---|
| 2xx (success) | No | Delivery confirmed |
| 4xx (client error) | **No** | Permanent endpoint misconfiguration; operator must fix subscription |
| **429** (rate limited) | **Yes** | Transient; backoff and retry |
| 5xx (server error) | **Yes** | Transient server failure; retry with backoff |
| Timeout / network error | **Yes** | Connection failure; retry with backoff |

> **Fail-closed posture**: The system does not silently downgrade to best-effort. Persistent failures are visible in the delivery audit log and will not be hidden or auto-resolved.

---

## Delivery Audit Records

Every delivery attempt (including retries) is recorded. Operators can query the audit log to confirm delivery status and diagnose failures.

### Query Delivery History

```http
GET /api/v1/webhooks/deliveries
Authorization: Bearer <jwt>
```

**Query parameters:**

| Parameter | Type | Description |
|---|---|---|
| `subscriptionId` | string | Filter by subscription |
| `eventId` | string | Filter by specific event |
| `success` | bool | Filter by delivery outcome |
| `fromDate` | ISO 8601 | Start of time window |
| `toDate` | ISO 8601 | End of time window |
| `page` | int | Page number (default: 1) |
| `pageSize` | int | Results per page (default: 50, max: 100) |

**Response:**

```json
{
  "success": true,
  "deliveries": [
    {
      "id": "del_xyz789",
      "subscriptionId": "c4b3e2a1-...",
      "eventId": "evt_abc123",
      "success": true,
      "statusCode": 200,
      "attemptedAt": "2026-03-15T02:00:05Z",
      "retryCount": 0,
      "errorMessage": null,
      "responseBody": "acknowledged",
      "willRetry": false,
      "nextRetryAt": null
    }
  ],
  "totalCount": 47,
  "successCount": 45,
  "failedCount": 2,
  "pendingRetries": 1
}
```

### Interpreting Failure Records

| Field | Value | Meaning |
|---|---|---|
| `success` | `false` | Delivery was not confirmed by endpoint |
| `willRetry` | `true` | Scheduled for automatic retry |
| `willRetry` | `false` | Permanently failed; operator action required |
| `statusCode` | 4xx | Endpoint configuration issue (auth, URL, route) |
| `statusCode` | 5xx | Transient server-side failure at receiver |
| `errorMessage` | non-null | Descriptive failure reason |

---

## Filtering

Subscriptions support two optional delivery filters:

- **`assetIdFilter`**: When set, the subscription only receives events for the specified asset ID. Leave null to receive events for all assets.
- **`networkFilter`**: When set, the subscription only receives events for the specified network (e.g. `"mainnet"`, `"testnet"`). Leave null to receive events across all networks.

Multiple event types can be subscribed in a single subscription, reducing the number of subscriptions needed for cross-category monitoring.

---

## Security Considerations

### Signing Secret Handling

- The signing secret is a 32-byte cryptographically random value encoded in Base64.
- It is returned **once** at subscription creation time. If lost, delete and recreate the subscription.
- Store signing secrets in secure secret stores (vault, KMS) — not in application configuration files.
- Rotate secrets by deleting and recreating the subscription.

### Receiver Best Practices

1. **Verify every signature** before processing the payload.
2. **Use HTTPS endpoints** in production; HTTP is only for development/internal use.
3. **Return 2xx immediately** after signature verification, then process asynchronously to avoid delivery timeouts.
4. **Deduplicate on `X-Webhook-Event-Id`** if your receiver may process duplicates (e.g. after a retry succeeds on both attempts).
5. **Validate the `EventType`** header before deserializing the `Data` block — new event types may be added in future releases.

### Sensitive Data Policy

Webhook payloads are designed for workflow automation and audit retention. They include:
- Asset identifiers and network context
- Status transitions (old → new)
- Actor addresses and verification timestamps
- Provider names (not provider-specific raw screening data)

Payloads do **not** include:
- Raw personal identification documents
- Provider-internal case IDs or raw screening details
- Private key material
- Credentials or API keys

---

## Safe Testing

### Local Development Receiver

Use a local HTTP relay (such as `ngrok`, `smee.io`, or a local test server) to receive and inspect payloads during development:

```bash
# Example using ngrok
ngrok http 8080
# Use the generated https URL as your subscription URL
```

### Minimal Test Receiver (C# ASP.NET)

```csharp
app.MapPost("/webhook", async (HttpContext ctx, IConfiguration config) =>
{
    var body = await new StreamReader(ctx.Request.Body).ReadToEndAsync();
    var signature = ctx.Request.Headers["X-Webhook-Signature"].FirstOrDefault();
    var eventType = ctx.Request.Headers["X-Webhook-Event-Type"].FirstOrDefault();

    var secret = config["Webhook:SigningSecret"]!;
    if (!VerifyWebhookSignature(body, secret, signature!))
        return Results.Unauthorized();

    Console.WriteLine($"Received {eventType}: {body}");
    return Results.Ok("acknowledged");
});
```

### Verifying End-to-End Delivery

1. Create a subscription with your test endpoint URL.
2. Trigger a compliance action (e.g. update KYC/compliance metadata via the API).
3. Check `GET /api/v1/webhooks/deliveries` to confirm a delivery attempt was recorded.
4. Verify your receiver logged the event with the correct signature.
5. Check that `success: true` in the delivery audit record.

---

## Configuration Reference

No additional configuration is required beyond standard API authentication. The webhook service is registered automatically in the application DI container. The in-memory repository is the default store; production deployments should replace it with a persistent implementation.

| Setting | Default | Description |
|---|---|---|
| Max retries | 3 | Maximum delivery retry attempts |
| Retry delay 1 | 1 minute | Delay before first retry |
| Retry delay 2 | 5 minutes | Delay before second retry |
| Retry delay 3 | 15 minutes | Delay before third retry |
| Request timeout | 30 seconds | HTTP client timeout per delivery attempt |
| Signing algorithm | HMAC-SHA256 | Signature generation algorithm |
| Max page size | 100 | Maximum page size for delivery history queries |

---

## API Endpoint Reference

| Method | Path | Description |
|---|---|---|
| `POST` | `/api/v1/webhooks/subscriptions` | Create a webhook subscription |
| `GET` | `/api/v1/webhooks/subscriptions` | List subscriptions for authenticated user |
| `GET` | `/api/v1/webhooks/subscriptions/{id}` | Get a specific subscription |
| `PUT` | `/api/v1/webhooks/subscriptions` | Update a subscription (activate/deactivate, change event types) |
| `DELETE` | `/api/v1/webhooks/subscriptions/{id}` | Delete a subscription |
| `GET` | `/api/v1/webhooks/deliveries` | Query delivery audit history |

All endpoints require JWT Bearer authentication (`Authorization: Bearer <token>`).

---

## Roadmap Alignment

This webhook system directly advances the Biatec Tokens roadmap goals:

- **Regulatory API coverage**: Structured, documented compliance event schema suitable for regulatory integration
- **Compliance Webhooks**: Full subscription management, signed delivery, retry, and audit persistence
- **Enterprise Features**: Delivery audit trail, operator-facing failure visibility, authentication/signing documentation suitable for enterprise vendor diligence

Future enhancements planned in this area:
- Persistent storage backend (database) for delivery audit records across restarts
- Replay tooling for re-delivering previously emitted events
- Rate-limiting and backpressure controls for high-volume deployments
- Dead-letter queue for permanently failed deliveries
- Event filtering by category (e.g. "only compliance events, not deployment events")

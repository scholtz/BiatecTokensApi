# Idempotency Keys and Replay Protection Implementation

## Overview

The BiatecTokensApi implements comprehensive idempotency support for all token deployment endpoints to prevent duplicate transactions and ensure safe, reliable processing of token operations. This feature addresses the critical business need for reliability in transactional systems, particularly when clients experience network issues, timeouts, or need to retry requests.

## Business Value

- **Prevents Duplicate Transactions**: Clients can safely retry requests without risk of creating duplicate tokens or executing duplicate operations
- **Reduces Support Costs**: Eliminates manual reconciliation and support tickets related to duplicate transactions
- **Improves User Trust**: Predictable, deterministic behavior builds confidence in the platform
- **Enterprise-Ready**: Aligns with industry best practices for payment and token platforms
- **Scalability**: Essential for high-traffic scenarios where retries are common

## Implementation Details

### Architecture

The idempotency implementation uses an attribute-based approach with in-memory caching:

- **IdempotencyKeyAttribute**: ASP.NET Core action filter that intercepts requests
- **In-Memory Cache**: ConcurrentDictionary for high-performance lookups
- **Request Hashing**: SHA-256 hash of request parameters for validation
- **Metrics Integration**: Tracks cache hits, misses, conflicts, and expirations
- **24-Hour Default Expiration**: Configurable per-endpoint if needed

### Supported Endpoints

All token deployment endpoints support idempotency:

#### ERC20 Tokens (Base Blockchain)
- `POST /api/v1/token/erc20-mintable/create`
- `POST /api/v1/token/erc20-preminted/create`

#### Algorand ASA Tokens
- `POST /api/v1/token/asa-ft/create` (Fungible Tokens)
- `POST /api/v1/token/asa-nft/create` (Non-Fungible Tokens)
- `POST /api/v1/token/asa-fnft/create` (Fractional NFTs)

#### Algorand ARC3 Tokens
- `POST /api/v1/token/arc3-ft/create` (Fungible Tokens)
- `POST /api/v1/token/arc3-nft/create` (Non-Fungible Tokens)
- `POST /api/v1/token/arc3-fnft/create` (Fractional NFTs)

#### Algorand ARC200 Tokens
- `POST /api/v1/token/arc200-mintable/create`
- `POST /api/v1/token/arc200-preminted/create`

#### Algorand ARC1400 Tokens
- `POST /api/v1/token/arc1400/create` (Security Tokens)

### API Usage

#### Request Format

Include an `Idempotency-Key` header with a unique identifier:

```http
POST /api/v1/token/erc20-mintable/create
Authorization: SigTx <signed-transaction>
Idempotency-Key: unique-deployment-id-12345
Content-Type: application/json

{
  "network": "base-mainnet",
  "name": "My Token",
  "symbol": "MTK",
  "cap": "1000000000000000000",
  "initialSupply": "500000000000000000"
}
```

#### Response Headers

The API returns these headers to indicate idempotency behavior:

- `X-Idempotency-Hit: false` - First request with this key (cache miss)
- `X-Idempotency-Hit: true` - Returning cached response (cache hit)
- `X-Correlation-ID: <uuid>` - For distributed tracing

#### Successful Replay

When a request is replayed with the same idempotency key and same parameters:

```http
HTTP/1.1 200 OK
X-Idempotency-Hit: true
X-Correlation-ID: 0HNJ5B48OPOES

{
  "success": true,
  "assetId": 12345,
  "transactionId": "abc123...",
  ...
}
```

#### Conflict Detection

When a request is replayed with the same idempotency key but **different parameters**:

```http
HTTP/1.1 400 Bad Request
Content-Type: application/json

{
  "success": false,
  "errorCode": "IDEMPOTENCY_KEY_MISMATCH",
  "errorMessage": "The provided idempotency key has been used with different request parameters. Please use a unique key for this request or reuse the same parameters.",
  "correlationId": "0HNJ5B48OPOES"
}
```

### Error Handling

#### Error Codes

- `IDEMPOTENCY_KEY_MISMATCH` (400) - Same key, different parameters
- Standard error codes apply for other validation/processing errors

#### No Idempotency Key

Requests without an `Idempotency-Key` header proceed normally without caching:

```http
POST /api/v1/token/erc20-mintable/create
Authorization: SigTx <signed-transaction>
Content-Type: application/json

{...}
```

This allows backwards compatibility for clients that don't need idempotency protection.

## Security Considerations

### Parameter Validation

The idempotency filter validates that cached requests match current request parameters using SHA-256 hashing. This prevents:

- **Logic Bypass**: Clients cannot reuse keys to skip business validation
- **Data Integrity**: Ensures cached responses match actual request intent
- **Audit Trail**: All mismatches are logged with correlation IDs

### Cache Security

- **In-Memory Only**: No persistent storage of sensitive request data
- **Automatic Expiration**: 24-hour default prevents indefinite storage
- **Periodic Cleanup**: Expired entries are removed automatically
- **Concurrent Safe**: Uses ConcurrentDictionary for thread safety

### Best Practices

1. **Unique Keys**: Use UUIDs or timestamp-based identifiers
2. **Client-Side Generation**: Let clients control idempotency keys
3. **Retry Logic**: Implement exponential backoff for retries
4. **Key Expiration**: Generate new keys after 24 hours
5. **Correlation IDs**: Track requests across distributed systems

## Observability

### Metrics

The implementation tracks the following metrics via IMetricsService:

- `idempotency_cache_hits_total` - Number of cached responses returned
- `idempotency_cache_misses_total` - Number of new requests cached
- `idempotency_conflicts_total` - Number of parameter mismatch errors
- `idempotency_expirations_total` - Number of expired cache entries

### Logging

All idempotency operations are logged at appropriate levels:

- **Debug**: Cache hits, misses, and expirations
- **Warning**: Conflict detection (key reuse with different parameters)
- **Info**: Metrics available via `/api/v1/metrics` endpoint

### Cache Statistics

Monitor cache health via the static method:

```csharp
var stats = IdempotencyKeyAttribute.GetCacheStatistics();
// Returns:
// {
//   "total_entries": 42,
//   "active_entries": 38,
//   "expired_entries": 4
// }
```

## Testing

### Unit Tests (IdempotencySecurityTests.cs)

8 comprehensive unit tests covering:
- Requests without idempotency keys
- Cache hit behavior (same key, same parameters)
- Conflict detection (same key, different parameters)
- Parameter order sensitivity
- Null vs empty string differentiation
- Cache expiration handling
- Response header verification

### Integration Tests (IdempotencyIntegrationTests.cs)

10 comprehensive integration tests covering:
- Header acceptance across all token types
- Cache behavior in real HTTP request flow
- Conflict detection with actual HTTP requests
- Cross-endpoint key scoping
- Requests without keys (backwards compatibility)
- Concurrent request handling
- Multi-token-type support verification

### Running Tests

```bash
# Run all idempotency tests
dotnet test --filter "Idempotency"

# Run unit tests only
dotnet test --filter "IdempotencySecurityTests"

# Run integration tests only
dotnet test --filter "IdempotencyIntegrationTests"
```

## Configuration

### Expiration Time

Default expiration is 24 hours. To customize per-endpoint:

```csharp
[IdempotencyKey(Expiration = 3600)] // 1 hour in seconds
[HttpPost("custom-endpoint")]
public async Task<IActionResult> CustomEndpoint([FromBody] Request request)
{
    // ...
}
```

### Feature Toggle

Idempotency is always available but opt-in via headers. No configuration required.

## Performance Considerations

### Memory Usage

- Each cached entry stores: key, response payload, status code, timestamp, request hash
- Automatic cleanup runs on ~1% of requests (probabilistic)
- Expired entries are removed during cleanup
- No unbounded growth risk

### Latency

- Cache lookup: O(1) via ConcurrentDictionary
- Hash computation: O(n) where n = request size
- Minimal overhead for cache misses (<1ms)
- No latency for cache hits (skip controller execution)

### Scalability

For high-traffic deployments with multiple instances:

- Current implementation uses in-memory cache (instance-scoped)
- Future enhancement: Redis-based distributed cache
- Clients should use consistent routing or accept cross-instance cache misses

## Compliance and Audit

### Audit Trail

All idempotency operations include:
- Correlation IDs for distributed tracing
- Logged parameter mismatches for security review
- Metrics for SLA monitoring

### GDPR/Data Retention

- Cached data expires after 24 hours (default)
- No persistent storage of user data in cache
- Request parameters are hashed (not stored in plain text)
- Complies with data minimization principles

## Future Enhancements

Potential improvements not in current scope:

1. **Database Persistence**: Store idempotency records for cross-instance support
2. **Redis Cache**: Distributed caching for multi-instance deployments
3. **Configurable Storage**: Choose between in-memory, Redis, or database
4. **Admin API**: Endpoint to view/clear cache statistics
5. **Webhook Support**: Extend idempotency to webhook delivery

## Acceptance Criteria Verification

✅ Backend accepts `Idempotency-Key` header on token-changing endpoints  
✅ Repeated requests with same key and payload return original response with HTTP 200  
✅ Repeated requests with same key but different payload return 400 error with IDEMPOTENCY_KEY_MISMATCH  
✅ Requests without idempotency key continue to behave normally  
✅ Idempotency records expire after configured window (24h default)  
✅ Metrics/logging show idempotency hit rates and conflicts  
✅ Unit tests cover middleware and storage layer behavior (8 tests)  
✅ Integration tests cover real endpoint behavior with retries (10 tests)  
✅ API documentation updated (this file + inline XML comments)  

## References

- Implementation: `BiatecTokensApi/Filters/IdempotencyAttribute.cs`
- Error Codes: `BiatecTokensApi/Models/ErrorCodes.cs`
- Unit Tests: `BiatecTokensTests/IdempotencySecurityTests.cs`
- Integration Tests: `BiatecTokensTests/IdempotencyIntegrationTests.cs`
- Usage: See controller XML documentation for each endpoint

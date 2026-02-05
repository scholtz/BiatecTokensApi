# Reliability and Observability Guide

## Overview

The BiatecTokensApi implements comprehensive reliability and observability features to ensure production-grade stability for token deployment, compliance checks, and wallet session flows.

## Features

### 1. Correlation ID Tracking

Every request is assigned a unique correlation ID for distributed tracing and incident investigation.

**Implementation:**
- `CorrelationIdMiddleware` ensures every request has a correlation ID
- Accepts `X-Correlation-ID` header from clients or generates a new one
- Adds correlation ID to response headers for client-side tracking
- Available through `HttpContext.TraceIdentifier` for logging

**Usage:**

```bash
# Client can provide correlation ID
curl -H "X-Correlation-ID: my-trace-id-123" https://api.example.com/api/v1/status

# Response includes correlation ID
X-Correlation-ID: my-trace-id-123
```

**Benefits:**
- End-to-end request tracing across services
- Simplified incident investigation
- Correlation of logs across distributed systems
- Client-side request tracking

### 2. Metrics Collection

Comprehensive metrics collection for monitoring API health, performance, and reliability.

**Endpoint:** `GET /api/v1/metrics`

**Metrics Categories:**

#### Counters
- `http_requests_total.{method}.{endpoint}` - Total HTTP requests
- `http_errors_total.{method}.{endpoint}.{errorCode}` - Total errors
- `http_errors_by_code.{errorCode}` - Errors grouped by code
- `token_deployments_total.{tokenType}.{status}` - Token deployments
- `rpc_calls_total.{network}.{operation}.{status}` - RPC calls
- `audit_writes_total.{category}.{status}` - Audit log writes

#### Histograms
- `http_request_duration_ms.{method}.{endpoint}` - Request latency distribution
- `token_deployment_duration_ms.{tokenType}` - Deployment duration
- `rpc_call_duration_ms.{network}.{operation}` - RPC call latency

#### Gauges
- `token_deployment_success_rate.{tokenType}` - Deployment success rate (0-1)
- `rpc_failure_rate.{network}` - RPC failure rate (0-1)

**Response Format:**

```json
{
  "counters": {
    "http_requests_total.GET.health": 150,
    "http_errors_total.GET.api.v1.token.404": 3,
    "token_deployments_total.ERC20.success": 45,
    "rpc_calls_total.algorand.getStatus.success": 120
  },
  "histograms": {
    "http_request_duration_ms.GET.api.v1.status": {
      "count": 100,
      "min": 15.2,
      "max": 350.5,
      "average": 87.3,
      "p50": 75.0,
      "p95": 200.5,
      "p99": 320.0
    }
  },
  "gauges": {
    "token_deployment_success_rate.ERC20": 0.95,
    "rpc_failure_rate.algorand": 0.02
  }
}
```

### 3. Automatic Metrics Collection

The `MetricsMiddleware` automatically tracks all HTTP requests without requiring manual instrumentation.

**What's Tracked:**
- Request count by endpoint and method
- Request latency distribution
- Error count by endpoint, method, and error code
- Response status codes

### 4. Service-Level Metrics

Services can record custom metrics using `IMetricsService`:

```csharp
public class TokenService
{
    private readonly IMetricsService _metrics;
    
    public async Task<DeploymentResult> DeployToken(...)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await DeployTokenAsync(...);
            stopwatch.Stop();
            
            // Record successful deployment
            _metrics.RecordDeployment("ERC20", true, stopwatch.Elapsed.TotalMilliseconds);
            
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            // Record failed deployment
            _metrics.RecordDeployment("ERC20", false, stopwatch.Elapsed.TotalMilliseconds);
            
            throw;
        }
    }
}
```

## Integration with Monitoring Systems

### Prometheus Integration

The metrics endpoint is compatible with Prometheus scraping:

```yaml
# prometheus.yml
scrape_configs:
  - job_name: 'biatec-tokens-api'
    scrape_interval: 15s
    metrics_path: '/api/v1/metrics'
    static_configs:
      - targets: ['api.example.com']
```

### Alerting Thresholds

Recommended alerting thresholds:

```yaml
# Alert on high error rate
- alert: HighErrorRate
  expr: rate(http_errors_total[5m]) > 0.05
  annotations:
    summary: "High error rate detected"

# Alert on slow response times
- alert: SlowResponseTime
  expr: histogram_quantile(0.95, http_request_duration_ms) > 2000
  annotations:
    summary: "P95 latency > 2 seconds"

# Alert on deployment failures
- alert: DeploymentFailures
  expr: token_deployment_success_rate < 0.90
  annotations:
    summary: "Token deployment success rate below 90%"

# Alert on RPC failures
- alert: RpcFailures
  expr: rpc_failure_rate > 0.10
  annotations:
    summary: "RPC failure rate above 10%"
```

## Health Monitoring

### Health Check Endpoints

1. **Basic Health**: `GET /health`
   - Simple health check for load balancers
   - Returns 200 if API is running

2. **Readiness**: `GET /health/ready`
   - Checks if API is ready to receive traffic
   - Verifies all dependencies (IPFS, Algorand, EVM, Stripe)
   - Returns 200 if ready, 503 if not ready

3. **Liveness**: `GET /health/live`
   - Verifies application is responsive
   - Does not check dependencies
   - Returns 200 if process is running

4. **Detailed Status**: `GET /api/v1/status`
   - Comprehensive status with component details
   - Includes version, uptime, environment
   - Provides health status for each dependency

### Health Check Components

- **IPFS**: Verifies IPFS service connectivity
- **Algorand Networks**: Checks all configured Algorand networks
- **EVM Chains**: Validates EVM RPC endpoint connectivity
- **Stripe**: Confirms payment service availability

## Error Handling

### Standardized Error Response

All API endpoints return consistent error responses:

```json
{
  "success": false,
  "errorCode": "BLOCKCHAIN_CONNECTION_ERROR",
  "errorMessage": "Failed to connect to Algorand network",
  "timestamp": "2026-02-05T19:25:00Z",
  "path": "/api/v1/token/deploy",
  "correlationId": "abc123-def456-ghi789",
  "remediationHint": "Check network status and try again",
  "details": {
    "network": "algorand",
    "endpoint": "https://mainnet-api.algonode.cloud"
  }
}
```

### Error Codes

See `Models/ErrorCodes.cs` for comprehensive list of error codes:

- **Validation Errors (400)**: `INVALID_REQUEST`, `MISSING_REQUIRED_FIELD`
- **Auth Errors (401, 403)**: `UNAUTHORIZED`, `FORBIDDEN`
- **Resource Errors (404, 409)**: `NOT_FOUND`, `CONFLICT`
- **External Service Errors (502, 503)**: `BLOCKCHAIN_CONNECTION_ERROR`, `IPFS_SERVICE_ERROR`
- **Blockchain Errors (422)**: `INSUFFICIENT_FUNDS`, `TRANSACTION_FAILED`
- **Server Errors (500)**: `INTERNAL_SERVER_ERROR`, `CONFIGURATION_ERROR`

## Incident Response

### Using Correlation IDs

1. User reports an error
2. User provides correlation ID from response
3. Search logs for correlation ID:
   ```bash
   grep "abc123-def456-ghi789" /var/log/api/*.log
   ```
4. Trace request through all services using same correlation ID

### Using Metrics for Investigation

1. Identify when issue started:
   ```bash
   curl https://api.example.com/api/v1/metrics | jq '.counters'
   ```

2. Check error rates by endpoint:
   ```bash
   curl https://api.example.com/api/v1/metrics | \
     jq '.counters | to_entries | map(select(.key | contains("error")))'
   ```

3. Analyze latency patterns:
   ```bash
   curl https://api.example.com/api/v1/metrics | \
     jq '.histograms | to_entries | map({endpoint: .key, p95: .value.P95})'
   ```

### Health Check Investigation

1. Check overall status:
   ```bash
   curl https://api.example.com/api/v1/status
   ```

2. Identify unhealthy components:
   ```bash
   curl https://api.example.com/api/v1/status | \
     jq '.components | to_entries | map(select(.value.status != "Healthy"))'
   ```

3. Monitor component recovery:
   ```bash
   watch -n 5 'curl -s https://api.example.com/api/v1/status | jq .components'
   ```

## Best Practices

### For API Developers

1. **Always use correlation IDs in logs:**
   ```csharp
   _logger.LogInformation("Processing request. CorrelationId: {CorrelationId}", 
       HttpContext.TraceIdentifier);
   ```

2. **Record metrics for critical operations:**
   ```csharp
   _metrics.RecordDeployment(tokenType, success, duration);
   _metrics.RecordRpcCall(network, operation, success, duration);
   ```

3. **Use standardized error responses:**
   ```csharp
   return ErrorResponseBuilder.BlockchainConnectionError("algorand", details);
   ```

4. **Include remediation hints:**
   ```csharp
   return new ApiErrorResponse
   {
       ErrorCode = "INSUFFICIENT_FUNDS",
       ErrorMessage = "Insufficient balance for transaction",
       RemediationHint = "Fund your account and try again"
   };
   ```

### For Operations Teams

1. **Set up Prometheus scraping** for continuous metrics collection
2. **Configure alerts** for critical thresholds
3. **Monitor health checks** in orchestration platform
4. **Aggregate logs** with correlation ID indexing
5. **Create dashboards** showing key metrics:
   - Request rate and latency
   - Error rate by endpoint
   - Deployment success rate
   - RPC health by network

### For Support Teams

1. **Always ask for correlation ID** when investigating issues
2. **Check metrics endpoint** for system-wide patterns
3. **Use status endpoint** to identify dependency issues
4. **Provide correlation ID to engineering** for deep investigation

## Testing

Run reliability and observability tests:

```bash
# Run metrics tests
dotnet test --filter "FullyQualifiedName~MetricsIntegrationTests"

# Run health check tests
dotnet test --filter "FullyQualifiedName~HealthCheckIntegrationTests"

# Run error handling tests
dotnet test --filter "FullyQualifiedName~ErrorHandlingIntegrationTests"
```

## Architecture

### Middleware Pipeline

```
Request
  ↓
CorrelationIdMiddleware (adds/preserves correlation ID)
  ↓
GlobalExceptionHandlerMiddleware (catches unhandled exceptions)
  ↓
MetricsMiddleware (tracks request metrics)
  ↓
RequestResponseLoggingMiddleware (logs requests)
  ↓
Authentication & Authorization
  ↓
Controllers
  ↓
Services (record custom metrics)
  ↓
Response (with correlation ID header)
```

### Services

- **MetricsService**: Thread-safe in-memory metrics storage
- **HealthCheckService**: Checks dependency health
- **IMetricsService**: Interface for recording custom metrics

## Future Enhancements

Planned improvements:

1. **Distributed Tracing**: Integration with OpenTelemetry for distributed tracing
2. **Persistent Metrics**: Store metrics in time-series database (e.g., InfluxDB)
3. **Custom Dashboards**: Pre-built Grafana dashboards
4. **Alerting Rules**: Pre-configured Prometheus alerting rules
5. **Audit Log Metrics**: Track audit write failures and retries
6. **Deployment Metrics**: Enhanced deployment tracking with stages
7. **SLA Tracking**: Automated SLA calculation and reporting

## Support

For questions or issues related to reliability and observability:

1. Check the metrics endpoint for system health
2. Review health check status for dependency issues
3. Use correlation IDs for incident investigation
4. Consult logs with correlation ID filtering
5. Contact the engineering team with relevant correlation IDs and metrics data

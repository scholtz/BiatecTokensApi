# Backend API Integration and Health Monitoring

## Overview

This document describes the stabilization improvements made to the BiatecTokensApi backend, including comprehensive health monitoring, enhanced error handling, and improved API reliability.

## Features Implemented

### 1. Comprehensive Health Checks

The API now includes detailed health checks for all external dependencies:

#### IPFS Health Check
- Monitors connectivity to the IPFS API endpoint
- Returns health status: Healthy, Degraded, or Unhealthy
- Includes API URL and status code in response details

#### Algorand Network Health Check
- Monitors all configured Algorand networks (mainnet, testnet, etc.)
- Checks each network's `/v2/status` endpoint
- Reports individual network status and overall health
- Provides network-specific details including server URLs

#### EVM Chain Health Check
- Monitors all configured EVM blockchain RPC endpoints
- Uses JSON-RPC `eth_blockNumber` call to verify connectivity
- Reports per-chain status and overall health
- Includes RPC URL and chain ID in response details

### 2. Health Check Endpoints

#### `/health` - Basic Health Check
Simple endpoint returning overall API health status. Returns:
- `Healthy` - All components are operational
- `Degraded` - Some components have issues but API is operational
- `Unhealthy` - Critical components are down

**Example Response:**
```
Healthy
```

#### `/health/ready` - Readiness Probe
Kubernetes-compatible readiness probe that checks if the API is ready to receive traffic. This endpoint runs all health checks and returns:
- `200 OK` - API is ready to serve requests
- `503 Service Unavailable` - API is not ready (dependencies unavailable)

**Use Case:** Configure as readiness probe in Kubernetes/Docker deployments

#### `/health/live` - Liveness Probe
Kubernetes-compatible liveness probe that verifies the API process is running. This endpoint does NOT run health checks and always returns:
- `200 OK` - API process is alive

**Use Case:** Configure as liveness probe in Kubernetes/Docker deployments to detect and restart crashed containers

#### `/api/v1/status` - Detailed Status Information
Comprehensive status endpoint providing detailed information about the API and all its components.

**Example Response:**
```json
{
  "status": "Healthy",
  "version": "1.0.0.0",
  "buildTime": "1.0.0+5c30a93",
  "timestamp": "2026-02-01T20:29:37Z",
  "uptime": "2:15:30",
  "environment": "Production",
  "components": {
    "ipfs": {
      "status": "Healthy",
      "message": "IPFS API is reachable",
      "details": {
        "apiUrl": "https://ipfs-api.biatec.io",
        "statusCode": 200
      }
    },
    "algorand": {
      "status": "Healthy",
      "message": "All 2 Algorand networks are healthy",
      "details": {
        "totalNetworks": 2,
        "healthyNetworks": 2,
        "unhealthyNetworks": 0,
        "network_wGHE2Pwd": {
          "status": "healthy",
          "server": "https://mainnet-api.4160.nodely.dev"
        },
        "network_SGO1GKSz": {
          "status": "healthy",
          "server": "https://testnet-api.4160.nodely.dev"
        }
      }
    },
    "evm": {
      "status": "Healthy",
      "message": "All 1 EVM chains are healthy",
      "details": {
        "totalChains": 1,
        "healthyChains": 1,
        "unhealthyChains": 0,
        "chain_8453": {
          "status": "healthy",
          "rpcUrl": "https://mainnet.base.org",
          "chainId": 8453
        }
      }
    }
  }
}
```

**Response Codes:**
- `200 OK` - All components are healthy or degraded
- `503 Service Unavailable` - One or more critical components are unhealthy

**Use Cases:**
- Monitoring dashboards
- Alerting systems
- Troubleshooting connectivity issues
- Verifying service configuration

### 3. Enhanced Error Handling

#### Global Exception Handler Middleware
Catches all unhandled exceptions and returns standardized error responses with:
- Error code for programmatic handling
- Human-readable error message
- Timestamp and correlation ID
- Request path
- Additional details (in development mode only)

**Example Error Response:**
```json
{
  "success": false,
  "errorCode": "TIMEOUT",
  "errorMessage": "The request timed out. Please try again later",
  "timestamp": "2026-02-01T20:30:00Z",
  "path": "/api/v1/token/create",
  "correlationId": "0HNJ1Q2DMT6T5:00000001"
}
```

**Error Codes:**
- `BAD_REQUEST` - Invalid request parameters (400)
- `UNAUTHORIZED` - Authentication required (401)
- `INVALID_OPERATION` - Operation not valid in current state (409)
- `TIMEOUT` - Request timed out (408)
- `EXTERNAL_SERVICE_ERROR` - External service communication failed (502)
- `INTERNAL_SERVER_ERROR` - Unexpected error (500)

**Security Features:**
- Stack traces and detailed error information only shown in Development environment
- Sensitive data never included in error responses
- Consistent error format across all endpoints

#### Request/Response Logging Middleware
Automatically logs all HTTP requests and responses with:
- HTTP method and path
- Status code
- Duration in milliseconds
- Correlation ID for request tracing

**Example Log Output:**
```
HTTP Request POST /api/v1/token/create started. CorrelationId: 0HNJ1Q2DMT6T5:00000001
HTTP Response POST /api/v1/token/create completed with status 200 in 245ms. CorrelationId: 0HNJ1Q2DMT6T5:00000001
```

**Benefits:**
- Easy debugging of API issues
- Performance monitoring
- Request tracing across logs
- Audit trail of API usage

### 4. API Stability Improvements

#### Timeout Configuration
- Health checks use 5-second timeouts to prevent hanging
- External API calls have configurable timeouts
- Prevents cascading failures from slow dependencies

#### Graceful Degradation
- API continues operating even when some dependencies are unavailable
- Health status accurately reflects component states
- Non-critical failures don't bring down the entire API

#### Correlation IDs
- Each request gets a unique correlation ID
- IDs are included in logs and error responses
- Makes it easy to trace requests across microservices

## Configuration

### Health Check Configuration
Health checks are automatically configured based on your `appsettings.json`:

```json
{
  "IPFSConfig": {
    "ApiUrl": "https://ipfs-api.biatec.io",
    "TimeoutSeconds": 30
  },
  "AlgorandAuthentication": {
    "AllowedNetworks": {
      "mainnet-genesis-hash": {
        "Server": "https://mainnet-api.4160.nodely.dev"
      }
    }
  },
  "EVMChains": [
    {
      "RpcUrl": "https://mainnet.base.org",
      "ChainId": 8453
    }
  ]
}
```

### Kubernetes Integration

#### Deployment Example
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: biatec-tokens-api
spec:
  template:
    spec:
      containers:
      - name: api
        image: biatec-tokens-api:latest
        ports:
        - containerPort: 7000
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

### Docker Compose Integration

```yaml
version: '3.8'
services:
  api:
    image: biatec-tokens-api:latest
    ports:
      - "7000:7000"
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:7000/health"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 40s
```

## Monitoring and Alerting

### Recommended Monitoring Setup

1. **Uptime Monitoring**
   - Monitor `/health/live` endpoint every 30 seconds
   - Alert if endpoint is unreachable for 2+ consecutive checks

2. **Dependency Health**
   - Monitor `/api/v1/status` endpoint every 60 seconds
   - Alert if any component status is "Unhealthy"
   - Warning if any component status is "Degraded" for > 5 minutes

3. **Response Time**
   - Track response time of `/api/v1/status` endpoint
   - Alert if p95 response time > 1 second

4. **Error Rates**
   - Monitor logs for error-level messages
   - Alert if error rate > 1% of requests

### Example Prometheus Metrics (Future Enhancement)
```
# HELP api_health_status Current health status (0=Unhealthy, 1=Degraded, 2=Healthy)
# TYPE api_health_status gauge
api_health_status{component="ipfs"} 2
api_health_status{component="algorand"} 2
api_health_status{component="evm"} 2

# HELP api_request_duration_seconds Request duration in seconds
# TYPE api_request_duration_seconds histogram
api_request_duration_seconds_bucket{method="GET",path="/api/v1/status",le="0.1"} 95
```

## Testing

### Integration Tests
8 comprehensive integration tests have been added:
- Basic health endpoint returns OK
- Readiness endpoint returns appropriate status
- Liveness endpoint always returns OK
- Status endpoint returns complete API status
- Status endpoint includes all component health
- Status endpoint returns consistent format
- Health endpoints accessible without authentication
- Status endpoint includes uptime metric

Run tests with:
```bash
dotnet test --filter "FullyQualifiedName~HealthCheckIntegrationTests"
```

### Manual Testing
```bash
# Start the API
dotnet run --project BiatecTokensApi/BiatecTokensApi.csproj

# Test basic health
curl http://localhost:5000/health

# Test readiness
curl http://localhost:5000/health/ready

# Test liveness
curl http://localhost:5000/health/live

# Test detailed status
curl http://localhost:5000/api/v1/status | jq '.'
```

## Security Considerations

1. **No Authentication Required**
   - Health endpoints are public and don't require authentication
   - This is intentional for monitoring systems to check health
   - No sensitive data is exposed in health responses

2. **Error Information**
   - Detailed stack traces only shown in Development environment
   - Production errors return generic messages
   - Correlation IDs allow debugging without exposing internals

3. **Rate Limiting (Recommended)**
   - Consider adding rate limiting to health endpoints
   - Prevents abuse of health check endpoints
   - Protects against DoS via health checks

## Troubleshooting

### Component Shows as Degraded
1. Check the component details in `/api/v1/status` response
2. Verify network connectivity to the external service
3. Check service configuration in `appsettings.json`
4. Review logs for specific error messages

### High Response Times
1. Check health of external dependencies
2. Review request logs for slow endpoints
3. Consider increasing timeout values
4. Check network latency to external services

### 503 Service Unavailable
1. Check `/api/v1/status` for component health details
2. Verify all critical dependencies are reachable
3. Review health check configuration
4. Check if services are running and accessible

## Future Enhancements

1. **Metrics Endpoint**
   - Add Prometheus metrics endpoint (`/metrics`)
   - Expose performance and health metrics
   - Enable advanced monitoring with Grafana

2. **Custom Health Checks**
   - Add database health check (when database is added)
   - Add cache health check (Redis/Memcached)
   - Add message queue health check

3. **Circuit Breaker Pattern**
   - Implement circuit breakers for external services
   - Automatically stop calling failing services
   - Improve resilience and response times

4. **Health Check Dashboard**
   - Web UI for viewing health status
   - Historical health data
   - Real-time component monitoring

## API Reliability Best Practices

This implementation follows industry best practices:

1. ✅ **Separation of Concerns** - Health checks separate from business logic
2. ✅ **Graceful Degradation** - API continues when non-critical components fail
3. ✅ **Explicit Error Handling** - All errors have clear codes and messages
4. ✅ **Request Tracing** - Correlation IDs for debugging
5. ✅ **Kubernetes Compatibility** - Proper liveness and readiness probes
6. ✅ **Observable** - Comprehensive logging and status information
7. ✅ **Secure** - No sensitive data in error responses
8. ✅ **Tested** - Comprehensive integration tests

## Conclusion

These enhancements significantly improve the reliability and observability of the BiatecTokensApi. The comprehensive health monitoring enables:
- Early detection of issues
- Better troubleshooting capabilities
- Improved uptime through proper Kubernetes integration
- Enhanced developer experience with clear error messages

For questions or issues, please refer to the main project README or open an issue on GitHub.

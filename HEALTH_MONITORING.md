# Health Monitoring Guide

## Overview

The BiatecTokensApi provides comprehensive health monitoring endpoints to check the status of the API and its dependencies. These endpoints are designed for monitoring systems, orchestration platforms, and troubleshooting.

## Health Check Endpoints

### 1. Basic Health Check

**Endpoint:** `GET /health`

**Purpose:** Simple health check to verify the API is running

**Response:**
- **200 OK**: API is healthy
- **503 Service Unavailable**: API is unhealthy

**Use Cases:**
- Load balancer health checks
- Simple uptime monitoring
- Kubernetes liveness probes

**Example:**
```bash
curl http://localhost:7000/health
```

**Response:**
```
Healthy
```

---

### 2. Readiness Check

**Endpoint:** `GET /health/ready`

**Purpose:** Checks if the API is ready to receive traffic (all dependencies available)

**Response:**
- **200 OK**: API is ready to serve requests
- **503 Service Unavailable**: API is not ready (dependencies unavailable)

**Checks Performed:**
- IPFS service connectivity
- Algorand network connectivity
- EVM blockchain connectivity

**Use Cases:**
- Kubernetes readiness probes
- Rolling deployment checks
- Traffic routing decisions

**Example:**
```bash
curl http://localhost:7000/health/ready
```

---

### 3. Liveness Check

**Endpoint:** `GET /health/live`

**Purpose:** Verifies the API process is alive and responsive

**Response:**
- **200 OK**: API process is running

**Note:** This endpoint does NOT check dependencies - it only verifies the application is running.

**Use Cases:**
- Kubernetes liveness probes
- Process monitoring
- Container restart decisions

**Example:**
```bash
curl http://localhost:7000/health/live
```

---

### 4. Detailed Status Endpoint

**Endpoint:** `GET /api/v1/status`

**Purpose:** Comprehensive status information with component-level health details

**Authentication:** Not required

**Response Format:**
```json
{
  "status": "Healthy",
  "version": "1.0.0",
  "buildTime": "2026-02-02T09:00:00Z",
  "timestamp": "2026-02-02T09:30:00Z",
  "uptime": "01:30:00",
  "environment": "Production",
  "components": {
    "ipfs": {
      "status": "Healthy",
      "message": "IPFS API is accessible",
      "details": {
        "responseTimeMs": 45,
        "endpoint": "https://ipfs-api.biatec.io"
      }
    },
    "algorand": {
      "status": "Healthy",
      "message": "Algorand network is accessible",
      "details": {
        "network": "mainnet",
        "responseTimeMs": 120,
        "lastBlockTime": "2026-02-02T09:29:55Z"
      }
    },
    "evm": {
      "status": "Healthy",
      "message": "EVM chain is accessible",
      "details": {
        "chainId": 8453,
        "chainName": "Base",
        "responseTimeMs": 89,
        "latestBlock": 12345678
      }
    }
  }
}
```

**HTTP Status Codes:**
- **200 OK**: All components healthy or degraded
- **503 Service Unavailable**: One or more critical components unhealthy

**Use Cases:**
- Monitoring dashboards
- Troubleshooting connectivity issues
- Performance monitoring
- Detailed health reporting

**Example:**
```bash
curl http://localhost:7000/api/v1/status | jq
```

---

## Component Health Status

Each component can have one of three statuses:

### 1. Healthy
- Component is fully operational
- All checks passed
- Response times within acceptable range

### 2. Degraded
- Component is operational but with issues
- Some checks failed but not critical
- Response times higher than normal

### 3. Unhealthy
- Component is not operational
- Critical checks failed
- Cannot serve requests

## Health Check Components

### IPFS Service (`ipfs`)

**What it checks:**
- IPFS API accessibility
- Response time
- Service availability

**Why it matters:**
- Required for ARC3 token metadata storage
- NFT image and metadata hosting
- Token documentation storage

**Troubleshooting:**
If IPFS is unhealthy:
1. Check IPFS service status
2. Verify IPFS configuration in `appsettings.json`
3. Check network connectivity to IPFS endpoint
4. Review IPFS service logs

---

### Algorand Network (`algorand`)

**What it checks:**
- Algorand node connectivity
- Network responsiveness
- Last block time
- API availability

**Why it matters:**
- Required for ASA, ARC3, ARC200, ARC1400 token deployments
- Transaction submission and confirmation
- Account operations

**Networks Supported:**
- Mainnet (`wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=`)
- Testnet
- Betanet
- Voimain
- Aramidmain

**Troubleshooting:**
If Algorand is unhealthy:
1. Check Algorand node status
2. Verify network configuration
3. Test node endpoint directly
4. Check for network congestion

---

### EVM Chain (`evm`)

**What it checks:**
- EVM RPC endpoint connectivity
- Chain ID verification
- Latest block retrieval
- Response time

**Why it matters:**
- Required for ERC20 token deployments
- Smart contract interactions
- Transaction submission on Base blockchain

**Supported Chains:**
- Base (Chain ID: 8453)
- Other EVM-compatible chains as configured

**Troubleshooting:**
If EVM is unhealthy:
1. Check RPC endpoint status
2. Verify chain configuration
3. Test RPC endpoint with curl
4. Check for rate limiting

---

## Monitoring Integration

### Prometheus

Health check endpoints can be scraped by Prometheus using the following configuration:

```yaml
scrape_configs:
  - job_name: 'biatec-tokens-api'
    metrics_path: '/health'
    static_configs:
      - targets: ['localhost:7000']
```

### Kubernetes

Example Kubernetes deployment with health checks:

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
          initialDelaySeconds: 30
          periodSeconds: 10
          timeoutSeconds: 5
          failureThreshold: 3
        readinessProbe:
          httpGet:
            path: /health/ready
            port: 7000
          initialDelaySeconds: 10
          periodSeconds: 5
          timeoutSeconds: 3
          failureThreshold: 2
```

### Docker Health Check

Docker container health check configuration:

```dockerfile
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD curl -f http://localhost:7000/health || exit 1
```

---

## Alerting Recommendations

### Critical Alerts (Immediate Response)

1. **API Down**: `/health` returns 503
   - Alert threshold: 2 consecutive failures
   - Action: Check API logs, restart if needed

2. **All Components Unhealthy**: `/api/v1/status` shows all components unhealthy
   - Alert threshold: 1 failure
   - Action: Emergency response, check infrastructure

### Warning Alerts (Investigate Soon)

1. **Component Degraded**: One component shows degraded status
   - Alert threshold: 5 consecutive minutes
   - Action: Investigate component, monitor trend

2. **High Response Time**: Component response time > 5 seconds
   - Alert threshold: 3 consecutive checks
   - Action: Check network, optimize if needed

3. **Single Component Unhealthy**: One component unhealthy but others healthy
   - Alert threshold: 2 consecutive failures
   - Action: Troubleshoot specific component

---

## Uptime Tracking

The `/api/v1/status` endpoint includes an `uptime` field showing how long the API has been running since the last restart.

**Example:**
```json
{
  "uptime": "5.12:34:56"  // 5 days, 12 hours, 34 minutes, 56 seconds
}
```

**Use Cases:**
- Track service restarts
- Calculate availability SLA
- Identify stability issues

---

## Best Practices

### 1. Regular Health Monitoring

- Monitor health endpoints every 30-60 seconds
- Track trends over time
- Set up alerts for anomalies

### 2. Component-Level Monitoring

- Monitor each component separately
- Track component-specific metrics
- Establish baseline performance

### 3. Graceful Degradation

- API can operate with some components degraded
- Prioritize critical components
- Implement fallback strategies

### 4. Correlation with Logs

- Cross-reference health check failures with logs
- Use timestamps to correlate events
- Track patterns in failures

### 5. Documentation Updates

- Keep health check documentation current
- Document new components
- Update troubleshooting guides

---

## Support

For health monitoring issues:

1. **Check Documentation**: Review this guide and ERROR_HANDLING.md
2. **Review Logs**: Check application logs for detailed error information
3. **Test Components**: Use manual testing commands to isolate issues
4. **Contact Support**: Provide health check output and logs

Support Email: support@biatec.io

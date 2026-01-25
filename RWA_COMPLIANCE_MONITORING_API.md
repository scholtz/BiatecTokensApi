# RWA Compliance Monitoring Dashboard API

## Overview

The RWA Compliance Monitoring Dashboard API provides enterprise-grade MICA/RWA compliance observability for the BiatecTokensApi platform. These endpoints expose whitelist enforcement metrics, audit log health status, and retention compliance per network (VOI/Aramid), enabling real-time compliance monitoring and regulatory reporting dashboards.

## Endpoints

### 1. Get Comprehensive Monitoring Metrics

**GET** `/api/v1/compliance/monitoring/metrics`

Retrieves comprehensive compliance monitoring metrics including whitelist enforcement, audit health, and retention status.

#### Authentication

Required: ARC-0014 authentication (Algorand Request for Comments 14)

#### Query Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| network | string | No | Filter by network (voimain-v1.0, aramidmain-v1.0, mainnet-v1.0, testnet-v1.0) |
| assetId | ulong | No | Filter by specific asset ID |
| fromDate | DateTime | No | Start date for metrics calculation (ISO 8601 format) |
| toDate | DateTime | No | End date for metrics calculation (ISO 8601 format) |

#### Response

**Success Response (200 OK):**

```json
{
  "success": true,
  "whitelistEnforcement": {
    "totalValidations": 1250,
    "allowedTransfers": 1050,
    "deniedTransfers": 200,
    "allowedPercentage": 84.0,
    "topDenialReasons": [
      {
        "reason": "Sender not whitelisted",
        "count": 120
      },
      {
        "reason": "Receiver not whitelisted",
        "count": 80
      }
    ],
    "assetsWithEnforcement": 15,
    "networkBreakdown": [
      {
        "network": "voimain-v1.0",
        "totalValidations": 750,
        "allowedTransfers": 630,
        "deniedTransfers": 120,
        "assetCount": 10
      },
      {
        "network": "aramidmain-v1.0",
        "totalValidations": 500,
        "allowedTransfers": 420,
        "deniedTransfers": 80,
        "assetCount": 5
      }
    ]
  },
  "auditHealth": {
    "totalEntries": 5000,
    "complianceEntries": 3000,
    "whitelistEntries": 2000,
    "oldestEntry": "2019-01-15T10:00:00Z",
    "newestEntry": "2026-01-25T14:30:00Z",
    "meetsRetentionRequirements": true,
    "status": "Healthy",
    "healthIssues": [],
    "coveragePercentage": 100.0
  },
  "networkRetentionStatus": [
    {
      "network": "voimain-v1.0",
      "requiresMicaCompliance": true,
      "totalAuditEntries": 3200,
      "oldestEntry": "2019-01-15T10:00:00Z",
      "retentionYears": 7,
      "meetsRetentionRequirements": true,
      "assetCount": 25,
      "assetsWithCompliance": 23,
      "complianceCoverage": 92.0,
      "status": "Active",
      "statusMessage": "Retention requirements met"
    },
    {
      "network": "aramidmain-v1.0",
      "requiresMicaCompliance": true,
      "totalAuditEntries": 1800,
      "oldestEntry": "2019-06-20T08:00:00Z",
      "retentionYears": 7,
      "meetsRetentionRequirements": true,
      "assetCount": 15,
      "assetsWithCompliance": 14,
      "complianceCoverage": 93.3,
      "status": "Active",
      "statusMessage": "Retention requirements met"
    }
  ],
  "overallHealthScore": 95,
  "calculatedAt": "2026-01-25T14:30:00Z"
}
```

**Error Response (500 Internal Server Error):**

```json
{
  "success": false,
  "errorMessage": "Failed to get monitoring metrics: [error details]"
}
```

### 2. Get Audit Log Health Status

**GET** `/api/v1/compliance/monitoring/audit-health`

Retrieves audit log health status including retention compliance and coverage metrics.

#### Authentication

Required: ARC-0014 authentication

#### Query Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| network | string | No | Filter by network |
| assetId | ulong | No | Filter by specific asset ID |

#### Response

**Success Response (200 OK):**

```json
{
  "success": true,
  "auditHealth": {
    "totalEntries": 5000,
    "complianceEntries": 3000,
    "whitelistEntries": 2000,
    "oldestEntry": "2019-01-15T10:00:00Z",
    "newestEntry": "2026-01-25T14:30:00Z",
    "meetsRetentionRequirements": true,
    "status": "Healthy",
    "healthIssues": [],
    "coveragePercentage": 100.0
  }
}
```

### 3. Get Retention Status Per Network

**GET** `/api/v1/compliance/monitoring/retention-status`

Retrieves per-network retention status with focus on VOI and Aramid networks for RWA compliance.

#### Authentication

Required: ARC-0014 authentication

#### Query Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| network | string | No | Filter by specific network (returns all if omitted) |

#### Response

**Success Response (200 OK):**

```json
{
  "success": true,
  "networks": [
    {
      "network": "voimain-v1.0",
      "requiresMicaCompliance": true,
      "totalAuditEntries": 3200,
      "oldestEntry": "2019-01-15T10:00:00Z",
      "retentionYears": 7,
      "meetsRetentionRequirements": true,
      "assetCount": 25,
      "assetsWithCompliance": 23,
      "complianceCoverage": 92.0,
      "status": "Active",
      "statusMessage": "Retention requirements met"
    },
    {
      "network": "aramidmain-v1.0",
      "requiresMicaCompliance": true,
      "totalAuditEntries": 1800,
      "oldestEntry": "2019-06-20T08:00:00Z",
      "retentionYears": 7,
      "meetsRetentionRequirements": true,
      "assetCount": 15,
      "assetsWithCompliance": 14,
      "complianceCoverage": 93.3,
      "status": "Active",
      "statusMessage": "Retention requirements met"
    },
    {
      "network": "mainnet-v1.0",
      "requiresMicaCompliance": false,
      "totalAuditEntries": 500,
      "oldestEntry": "2020-03-10T12:00:00Z",
      "retentionYears": 7,
      "meetsRetentionRequirements": false,
      "assetCount": 8,
      "assetsWithCompliance": 4,
      "complianceCoverage": 50.0,
      "status": "Warning",
      "statusMessage": "Low compliance coverage: 50.0%"
    }
  ],
  "overallRetentionScore": 82
}
```

## Response Models

### WhitelistEnforcementMetrics

| Field | Type | Description |
|-------|------|-------------|
| totalValidations | integer | Total number of transfer validations performed |
| allowedTransfers | integer | Number of transfers that were allowed |
| deniedTransfers | integer | Number of transfers that were denied |
| allowedPercentage | decimal | Percentage of transfers allowed (0-100) |
| topDenialReasons | DenialReasonCount[] | Top denial reasons with occurrence counts |
| assetsWithEnforcement | integer | Number of unique assets with enforcement enabled |
| networkBreakdown | NetworkEnforcementMetrics[] | Enforcement breakdown by network |

### NetworkEnforcementMetrics

| Field | Type | Description |
|-------|------|-------------|
| network | string | Network name (voimain-v1.0, aramidmain-v1.0, etc.) |
| totalValidations | integer | Total validations on this network |
| allowedTransfers | integer | Allowed transfers on this network |
| deniedTransfers | integer | Denied transfers on this network |
| assetCount | integer | Number of assets on this network |

### AuditLogHealth

| Field | Type | Description |
|-------|------|-------------|
| totalEntries | integer | Total number of audit log entries |
| complianceEntries | integer | Number of compliance audit entries |
| whitelistEntries | integer | Number of whitelist audit entries |
| oldestEntry | DateTime? | Oldest audit entry timestamp |
| newestEntry | DateTime? | Most recent audit entry timestamp |
| meetsRetentionRequirements | boolean | Whether audit logs meet MICA 7-year retention requirement |
| status | AuditHealthStatus | Health status (Healthy, Warning, Critical) |
| healthIssues | string[] | List of health issues if any |
| coveragePercentage | decimal | Audit coverage percentage (0-100) |

### AuditHealthStatus Enum

- **Healthy**: All audit logs are healthy and meet requirements
- **Warning**: Some audit logs have minor issues
- **Critical**: Critical audit log issues detected

### NetworkRetentionStatus

| Field | Type | Description |
|-------|------|-------------|
| network | string | Network name |
| requiresMicaCompliance | boolean | Whether this network requires MICA compliance |
| totalAuditEntries | integer | Total audit entries for this network |
| oldestEntry | DateTime? | Oldest audit entry for this network |
| retentionYears | integer | Retention period in years (default: 7) |
| meetsRetentionRequirements | boolean | Whether retention requirements are met |
| assetCount | integer | Number of assets on this network |
| assetsWithCompliance | integer | Number of assets with compliance metadata |
| complianceCoverage | decimal | Compliance coverage percentage (0-100) |
| status | RetentionStatus | Retention status (Active, Warning, Critical) |
| statusMessage | string? | Human-readable status message |

### RetentionStatus Enum

- **Active**: Retention requirements are met
- **Warning**: Approaching retention limits or low coverage
- **Critical**: Retention requirements not met

## Overall Health Score Calculation

The overall health score (0-100) is calculated based on:

| Component | Weight | Description |
|-----------|--------|-------------|
| Audit Health Status | 40% | Healthy=40pts, Warning=20pts, Critical=0pts |
| Retention Compliance | 40% | Based on per-network retention scores |
| Enforcement Activity | 20% | Active enforcement=20pts, Assets configured=10pts |

### Score Interpretation

- **90-100**: Excellent - Full compliance infrastructure with strong enforcement
- **75-89**: Good - Solid compliance with minor gaps
- **60-74**: Fair - Basic compliance with improvement needed
- **40-59**: Poor - Significant compliance gaps
- **0-39**: Critical - Major compliance issues requiring immediate attention

## MICA Compliance Requirements

### 7-Year Audit Retention

MiCA (Markets in Crypto-Assets Regulation) requires maintaining immutable audit logs for a minimum of 7 years. The API automatically checks:

1. **Oldest Entry Age**: Confirms audit logs extend back at least 7 years (or to deployment date if newer)
2. **Entry Completeness**: Validates presence of both compliance and whitelist audit entries
3. **Retention Status**: Calculates per-network retention compliance

### VOI and Aramid Network Focus

VOI (voimain-v1.0) and Aramid (aramidmain-v1.0) networks are flagged as requiring MICA compliance:

- `requiresMicaCompliance: true` for these networks
- Higher scrutiny on retention and coverage metrics
- Separate status reporting per network

## Use Cases

### Compliance Dashboard

Build a real-time compliance monitoring dashboard:

```typescript
// Fetch comprehensive monitoring metrics
const response = await fetch('/api/v1/compliance/monitoring/metrics?network=voimain-v1.0', {
  headers: {
    'Authorization': `SigTx ${signedTransaction}`
  }
});

const metrics = await response.json();

// Display overall health score
renderHealthScore(metrics.overallHealthScore);

// Display enforcement metrics
renderEnforcementChart({
  allowed: metrics.whitelistEnforcement.allowedTransfers,
  denied: metrics.whitelistEnforcement.deniedTransfers
});

// Display top denial reasons
renderDenialReasons(metrics.whitelistEnforcement.topDenialReasons);

// Display network retention status
renderNetworkStatus(metrics.networkRetentionStatus);
```

### Audit Health Monitoring

Monitor audit log health for regulatory compliance:

```typescript
const response = await fetch('/api/v1/compliance/monitoring/audit-health', {
  headers: {
    'Authorization': `SigTx ${signedTransaction}`
  }
});

const { auditHealth } = await response.json();

// Check retention compliance
if (!auditHealth.meetsRetentionRequirements) {
  showAlert('Audit logs do not meet 7-year MICA retention requirement');
}

// Display health status
const statusColor = auditHealth.status === 'Healthy' ? 'green' : 
                    auditHealth.status === 'Warning' ? 'yellow' : 'red';
renderStatusBadge(auditHealth.status, statusColor);

// Show health issues
if (auditHealth.healthIssues.length > 0) {
  displayIssues(auditHealth.healthIssues);
}
```

### Network Retention Reporting

Generate per-network retention compliance reports:

```typescript
const response = await fetch('/api/v1/compliance/monitoring/retention-status', {
  headers: {
    'Authorization': `SigTx ${signedTransaction}`
  }
});

const { networks, overallRetentionScore } = await response.json();

// Generate report for VOI and Aramid networks
const micaNetworks = networks.filter(n => n.requiresMicaCompliance);

micaNetworks.forEach(network => {
  const report = {
    network: network.network,
    compliance: network.complianceCoverage.toFixed(1) + '%',
    retentionStatus: network.meetsRetentionRequirements ? '✓ Met' : '✗ Not Met',
    auditEntries: network.totalAuditEntries,
    statusMessage: network.statusMessage
  };
  
  generateNetworkReport(report);
});
```

## Integration with Existing APIs

The monitoring endpoints complement existing compliance APIs:

- **Compliance Metadata API**: `/api/v1/compliance/{assetId}` - Create/update compliance metadata
- **Compliance Reports API**: `/api/v1/compliance/report` - Generate comprehensive compliance reports
- **Compliance Indicators API**: `/api/v1/token/{assetId}/compliance-indicators` - Get simplified indicators
- **Whitelist Audit API**: `/api/v1/whitelist/audit-log` - Retrieve whitelist audit logs

## Performance Considerations

### Page Size Limits

The monitoring endpoints use pagination internally with optimized page sizes:

- Enforcement metrics: 1000 validation entries
- Audit health checks: 100 entries per source
- Retention status: 1000 metadata entries per network

### Response Time

Typical response times:
- Monitoring metrics: 200-500ms (depending on data volume)
- Audit health: 100-300ms
- Retention status: 150-400ms

### Caching Recommendations

For dashboard displays, consider caching monitoring metrics:
- Cache duration: 5-15 minutes
- Invalidate on compliance metadata changes
- Use network-specific cache keys

## Error Handling

All endpoints return consistent error responses:

```json
{
  "success": false,
  "errorMessage": "Detailed error message"
}
```

Common error scenarios:
- **401 Unauthorized**: Missing or invalid ARC-0014 authentication
- **500 Internal Server Error**: Server-side processing error

## Security

- **Authentication**: All endpoints require ARC-0014 authentication
- **Authorization**: Users can only view metrics for their own tokens
- **Rate Limiting**: Standard API rate limits apply
- **Audit Logging**: All monitoring API calls are logged for security auditing

## Related Documentation

- [Compliance API](./COMPLIANCE_API.md) - Main compliance metadata API
- [Compliance Reports API](./VOI_ARAMID_COMPLIANCE_REPORT_API.md) - Comprehensive compliance reporting
- [Compliance Indicators API](./COMPLIANCE_INDICATORS_API.md) - Simplified compliance indicators
- [Whitelist Feature](./WHITELIST_FEATURE.md) - Whitelist management
- [Enterprise Audit API](./ENTERPRISE_AUDIT_API.md) - Enterprise audit logging
- [MICA Compliance Signals Roadmap](./MICA_COMPLIANCE_SIGNALS_API_ROADMAP.md) - Overall compliance architecture

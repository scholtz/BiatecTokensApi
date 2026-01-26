# Dashboard Integration Quick Start Guide

## Overview

This guide provides practical examples for integrating the BiatecTokensApi whitelist enforcement and compliance audit features into your MICA compliance dashboard. All APIs are production-ready and fully tested.

## Authentication

All API calls require ARC-0014 authentication:

```javascript
const headers = {
  'Authorization': `SigTx ${signedTransaction}`,
  'Content-Type': 'application/json'
};
```

## Base URL

```
Production: https://api.biatec.io
Staging: https://staging-api.biatec.io
Local Dev: https://localhost:7000
```

## Quick Start Examples

### 1. Display Recent Audit Events

**Endpoint:** `GET /api/v1/enterprise-audit/export`

**JavaScript Example:**
```javascript
async function fetchRecentAuditEvents() {
  const response = await fetch(
    'https://api.biatec.io/api/v1/enterprise-audit/export?page=1&pageSize=20',
    { headers }
  );
  
  const data = await response.json();
  
  // Display in dashboard
  data.entries.forEach(entry => {
    console.log({
      time: entry.performedAt,
      network: entry.network,
      category: entry.category,
      action: entry.actionType,
      status: entry.success ? '✓' : '✗',
      user: entry.performedBy
    });
  });
  
  // Show summary stats
  console.log('Summary:', data.summary);
}
```

**React Component Example:**
```jsx
import { useState, useEffect } from 'react';

function AuditLogDashboard() {
  const [auditData, setAuditData] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    fetch('/api/v1/enterprise-audit/export?page=1&pageSize=20', { headers })
      .then(res => res.json())
      .then(data => {
        setAuditData(data);
        setLoading(false);
      });
  }, []);

  if (loading) return <div>Loading...</div>;

  return (
    <div>
      <h2>Recent Audit Events</h2>
      <table>
        <thead>
          <tr>
            <th>Time</th>
            <th>Network</th>
            <th>Category</th>
            <th>Action</th>
            <th>Status</th>
          </tr>
        </thead>
        <tbody>
          {auditData.entries.map(entry => (
            <tr key={entry.id}>
              <td>{new Date(entry.performedAt).toLocaleString()}</td>
              <td>{entry.network}</td>
              <td>{entry.category}</td>
              <td>{entry.actionType}</td>
              <td>{entry.success ? '✓' : '✗'}</td>
            </tr>
          ))}
        </tbody>
      </table>
      
      <div className="summary">
        <h3>Summary</h3>
        <p>Total Events: {auditData.totalCount}</p>
        <p>Successful: {auditData.summary.successfulOperations}</p>
        <p>Failed: {auditData.summary.failedOperations}</p>
        <p>Networks: {auditData.summary.networks.join(', ')}</p>
      </div>
    </div>
  );
}
```

### 2. Filter by VOI Network

**Endpoint:** `GET /api/v1/enterprise-audit/export?network=voimain-v1.0`

```javascript
async function fetchVOIAuditLogs() {
  const response = await fetch(
    'https://api.biatec.io/api/v1/enterprise-audit/export?network=voimain-v1.0&page=1&pageSize=50',
    { headers }
  );
  
  const data = await response.json();
  return data.entries; // All entries will have network: "voimain-v1.0"
}
```

### 3. Filter by Aramid Network

**Endpoint:** `GET /api/v1/enterprise-audit/export?network=aramidmain-v1.0`

```javascript
async function fetchAramidAuditLogs() {
  const response = await fetch(
    'https://api.biatec.io/api/v1/enterprise-audit/export?network=aramidmain-v1.0&page=1&pageSize=50',
    { headers }
  );
  
  const data = await response.json();
  return data.entries; // All entries will have network: "aramidmain-v1.0"
}
```

### 4. Monitor Failed Transfer Validations

**Endpoint:** `GET /api/v1/enterprise-audit/export?category=TransferValidation&success=false`

```javascript
async function monitorFailedTransfers() {
  const response = await fetch(
    'https://api.biatec.io/api/v1/enterprise-audit/export?category=TransferValidation&success=false&page=1&pageSize=50',
    { headers }
  );
  
  const data = await response.json();
  
  // Show alerts for each failed transfer
  data.entries.forEach(entry => {
    showAlert({
      title: 'Transfer Blocked',
      message: entry.denialReason,
      network: entry.network,
      asset: entry.assetId,
      address: entry.affectedAddress,
      time: entry.performedAt
    });
  });
}
```

**React Alert Component:**
```jsx
function ComplianceAlerts() {
  const [alerts, setAlerts] = useState([]);

  useEffect(() => {
    const interval = setInterval(async () => {
      const response = await fetch(
        '/api/v1/enterprise-audit/export?category=TransferValidation&success=false&page=1&pageSize=10',
        { headers }
      );
      const data = await response.json();
      setAlerts(data.entries);
    }, 30000); // Check every 30 seconds

    return () => clearInterval(interval);
  }, []);

  return (
    <div className="alerts">
      <h3>⚠️ Compliance Alerts</h3>
      {alerts.map(alert => (
        <div key={alert.id} className="alert alert-warning">
          <strong>Transfer Blocked on {alert.network}</strong>
          <p>Asset: {alert.assetId}</p>
          <p>Address: {alert.affectedAddress}</p>
          <p>Reason: {alert.denialReason}</p>
          <small>{new Date(alert.performedAt).toLocaleString()}</small>
        </div>
      ))}
    </div>
  );
}
```

### 5. Network Comparison Widget

```javascript
async function compareNetworkActivity() {
  // Fetch VOI activity
  const voiResponse = await fetch(
    '/api/v1/enterprise-audit/export?network=voimain-v1.0',
    { headers }
  );
  const voiData = await voiResponse.json();
  
  // Fetch Aramid activity
  const aramidResponse = await fetch(
    '/api/v1/enterprise-audit/export?network=aramidmain-v1.0',
    { headers }
  );
  const aramidData = await aramidResponse.json();
  
  return {
    voi: {
      totalEvents: voiData.totalCount,
      successful: voiData.summary.successfulOperations,
      failed: voiData.summary.failedOperations,
      assets: voiData.summary.assets.length
    },
    aramid: {
      totalEvents: aramidData.totalCount,
      successful: aramidData.summary.successfulOperations,
      failed: aramidData.summary.failedOperations,
      assets: aramidData.summary.assets.length
    }
  };
}
```

**React Comparison Component:**
```jsx
function NetworkComparison() {
  const [data, setData] = useState(null);

  useEffect(() => {
    compareNetworkActivity().then(setData);
  }, []);

  if (!data) return <div>Loading...</div>;

  return (
    <div className="network-comparison">
      <h3>Network Activity Comparison</h3>
      <div className="comparison-grid">
        <div className="network-card">
          <h4>VOI Mainnet</h4>
          <p>Total Events: {data.voi.totalEvents}</p>
          <p>Success Rate: {((data.voi.successful / data.voi.totalEvents) * 100).toFixed(1)}%</p>
          <p>Active Assets: {data.voi.assets}</p>
        </div>
        <div className="network-card">
          <h4>Aramid Mainnet</h4>
          <p>Total Events: {data.aramid.totalEvents}</p>
          <p>Success Rate: {((data.aramid.successful / data.aramid.totalEvents) * 100).toFixed(1)}%</p>
          <p>Active Assets: {data.aramid.assets}</p>
        </div>
      </div>
    </div>
  );
}
```

### 6. Export Compliance Report

**CSV Export:**
```javascript
async function exportComplianceReport(assetId, startDate, endDate) {
  const params = new URLSearchParams({
    assetId: assetId,
    fromDate: startDate,
    toDate: endDate
  });
  
  // Download CSV file
  window.location.href = `/api/v1/enterprise-audit/export/csv?${params}`;
}

// Usage
exportComplianceReport(12345, '2025-01-01', '2026-01-26');
```

**JSON Export:**
```javascript
async function exportComplianceJSON(network, startDate) {
  const params = new URLSearchParams({
    network: network,
    fromDate: startDate
  });
  
  const response = await fetch(
    `/api/v1/enterprise-audit/export/json?${params}`,
    { headers }
  );
  
  const blob = await response.blob();
  const url = window.URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = `${network}-audit-${new Date().toISOString()}.json`;
  a.click();
}

// Usage
exportComplianceJSON('voimain-v1.0', '2025-01-01');
```

### 7. Validate Transfer Before Execution

**Endpoint:** `POST /api/v1/whitelist/validate-transfer`

```javascript
async function validateTransferBeforeExecution(assetId, fromAddress, toAddress) {
  const response = await fetch(
    '/api/v1/whitelist/validate-transfer',
    {
      method: 'POST',
      headers,
      body: JSON.stringify({
        assetId: assetId,
        fromAddress: fromAddress,
        toAddress: toAddress
      })
    }
  );
  
  const result = await response.json();
  
  if (result.isAllowed) {
    console.log('✓ Transfer allowed - proceed with blockchain transaction');
    return true;
  } else {
    console.error('✗ Transfer blocked:', result.denialReason);
    alert(`Transfer not allowed: ${result.denialReason}`);
    return false;
  }
}

// Usage in transfer workflow
async function executeTransfer(assetId, fromAddress, toAddress, amount) {
  // First, validate with API
  const isAllowed = await validateTransferBeforeExecution(assetId, fromAddress, toAddress);
  
  if (!isAllowed) {
    return { success: false, error: 'Transfer blocked by whitelist' };
  }
  
  // If allowed, proceed with blockchain transaction
  const txResult = await sendBlockchainTransaction({
    assetId,
    fromAddress,
    toAddress,
    amount
  });
  
  return txResult;
}
```

### 8. Real-Time Dashboard Widget

**Complete Dashboard Component:**
```jsx
import { useState, useEffect } from 'react';

function ComplianceDashboard() {
  const [recentEvents, setRecentEvents] = useState([]);
  const [alerts, setAlerts] = useState([]);
  const [networkStats, setNetworkStats] = useState(null);
  const [loading, setLoading] = useState(true);

  // Fetch all data on mount and refresh every 30 seconds
  useEffect(() => {
    const fetchDashboardData = async () => {
      try {
        // Recent events
        const eventsRes = await fetch(
          '/api/v1/enterprise-audit/export?page=1&pageSize=10',
          { headers }
        );
        const eventsData = await eventsRes.json();
        setRecentEvents(eventsData.entries);

        // Failed transfers (alerts)
        const alertsRes = await fetch(
          '/api/v1/enterprise-audit/export?category=TransferValidation&success=false&page=1&pageSize=5',
          { headers }
        );
        const alertsData = await alertsRes.json();
        setAlerts(alertsData.entries);

        // Network comparison
        const voiRes = await fetch(
          '/api/v1/enterprise-audit/export?network=voimain-v1.0',
          { headers }
        );
        const voiData = await voiRes.json();

        const aramidRes = await fetch(
          '/api/v1/enterprise-audit/export?network=aramidmain-v1.0',
          { headers }
        );
        const aramidData = await aramidRes.json();

        setNetworkStats({
          voi: voiData.summary,
          aramid: aramidData.summary
        });

        setLoading(false);
      } catch (error) {
        console.error('Error fetching dashboard data:', error);
      }
    };

    fetchDashboardData();
    const interval = setInterval(fetchDashboardData, 30000);

    return () => clearInterval(interval);
  }, []);

  if (loading) {
    return <div className="loading">Loading compliance dashboard...</div>;
  }

  return (
    <div className="compliance-dashboard">
      <h1>MICA Compliance Dashboard</h1>
      
      {/* Alerts Section */}
      <section className="alerts-section">
        <h2>⚠️ Compliance Alerts</h2>
        {alerts.length === 0 ? (
          <p className="no-alerts">✓ No compliance issues detected</p>
        ) : (
          alerts.map(alert => (
            <div key={alert.id} className="alert">
              <strong>Transfer Blocked - {alert.network}</strong>
              <p>Asset: {alert.assetId}</p>
              <p>Address: {alert.affectedAddress}</p>
              <p>Reason: {alert.denialReason}</p>
              <small>{new Date(alert.performedAt).toLocaleString()}</small>
            </div>
          ))
        )}
      </section>

      {/* Network Stats Section */}
      <section className="network-stats">
        <h2>Network Activity</h2>
        <div className="stats-grid">
          <div className="stat-card">
            <h3>VOI Mainnet</h3>
            <p className="stat-number">{networkStats.voi.whitelistEvents + networkStats.voi.complianceEvents}</p>
            <p className="stat-label">Total Events</p>
            <p className="stat-success">✓ {networkStats.voi.successfulOperations} successful</p>
            <p className="stat-failed">✗ {networkStats.voi.failedOperations} failed</p>
          </div>
          <div className="stat-card">
            <h3>Aramid Mainnet</h3>
            <p className="stat-number">{networkStats.aramid.whitelistEvents + networkStats.aramid.complianceEvents}</p>
            <p className="stat-label">Total Events</p>
            <p className="stat-success">✓ {networkStats.aramid.successfulOperations} successful</p>
            <p className="stat-failed">✗ {networkStats.aramid.failedOperations} failed</p>
          </div>
        </div>
      </section>

      {/* Recent Events Section */}
      <section className="recent-events">
        <h2>Recent Audit Events</h2>
        <table>
          <thead>
            <tr>
              <th>Time</th>
              <th>Network</th>
              <th>Category</th>
              <th>Action</th>
              <th>Status</th>
            </tr>
          </thead>
          <tbody>
            {recentEvents.map(event => (
              <tr key={event.id}>
                <td>{new Date(event.performedAt).toLocaleString()}</td>
                <td>
                  <span className={`network-badge ${event.network}`}>
                    {event.network}
                  </span>
                </td>
                <td>{event.category}</td>
                <td>{event.actionType}</td>
                <td>
                  {event.success ? (
                    <span className="status-success">✓</span>
                  ) : (
                    <span className="status-failed">✗</span>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>

      {/* Export Section */}
      <section className="export-section">
        <h2>Export Compliance Reports</h2>
        <button onClick={() => window.location.href = '/api/v1/enterprise-audit/export/csv'}>
          📊 Export as CSV
        </button>
        <button onClick={() => window.location.href = '/api/v1/enterprise-audit/export/json'}>
          📄 Export as JSON
        </button>
      </section>
    </div>
  );
}

export default ComplianceDashboard;
```

## Query Parameters Reference

### Available Filters

| Parameter | Type | Description | Example |
|-----------|------|-------------|---------|
| `assetId` | number | Filter by token asset ID | `12345` |
| `network` | string | Filter by network | `voimain-v1.0`, `aramidmain-v1.0` |
| `category` | string | Filter by event category | `Whitelist`, `TransferValidation`, `Compliance` |
| `actionType` | string | Filter by action | `Add`, `Update`, `Remove` |
| `performedBy` | string | Filter by user address | `ADDR123...` |
| `affectedAddress` | string | Filter by affected address | `ADDR456...` |
| `success` | boolean | Filter by result | `true`, `false` |
| `fromDate` | string | Start date (ISO 8601) | `2026-01-01T00:00:00Z` |
| `toDate` | string | End date (ISO 8601) | `2026-01-26T23:59:59Z` |
| `page` | number | Page number | `1`, `2`, `3` |
| `pageSize` | number | Results per page (max 100) | `20`, `50`, `100` |

### Network Identifiers

| Network | Identifier |
|---------|------------|
| VOI Mainnet | `voimain-v1.0` |
| Aramid Mainnet | `aramidmain-v1.0` |
| Algorand Mainnet | `mainnet-v1.0` |
| Algorand Testnet | `testnet-v1.0` |
| Base Mainnet | `base-mainnet` |

### Event Categories

- `Whitelist` - Whitelist management events
- `Blacklist` - Blacklist management events
- `Compliance` - Compliance metadata events
- `TransferValidation` - Transfer validation events
- `TokenIssuance` - Token deployment events
- `WhitelistRules` - Whitelist rule configuration

## Response Structure

All audit endpoints return this structure:

```typescript
interface AuditLogResponse {
  success: boolean;
  entries: AuditLogEntry[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  retentionPolicy: {
    minimumRetentionYears: number;
    regulatoryFramework: string;
    immutableEntries: boolean;
    description: string;
  };
  summary: {
    whitelistEvents: number;
    blacklistEvents: number;
    complianceEvents: number;
    tokenIssuanceEvents: number;
    transferValidationEvents: number;
    successfulOperations: number;
    failedOperations: number;
    networks: string[];
    assets: number[];
    dateRange: {
      earliestEvent: string;
      latestEvent: string;
    };
  };
}

interface AuditLogEntry {
  id: string;
  assetId?: number;
  network?: string;
  category: string;
  actionType: string;
  performedBy: string;
  performedAt: string;
  success: boolean;
  errorMessage?: string;
  affectedAddress?: string;
  oldStatus?: string;
  newStatus?: string;
  notes?: string;
  toAddress?: string;
  transferAllowed?: boolean;
  denialReason?: string;
  amount?: number;
  role?: string;
  correlationId?: string;
}
```

## Error Handling

```javascript
async function fetchAuditLogsWithErrorHandling() {
  try {
    const response = await fetch(
      '/api/v1/enterprise-audit/export?page=1&pageSize=20',
      { headers }
    );
    
    if (!response.ok) {
      if (response.status === 401) {
        throw new Error('Authentication failed - please sign in again');
      } else if (response.status === 403) {
        throw new Error('Insufficient permissions to access audit logs');
      } else if (response.status === 400) {
        const error = await response.json();
        throw new Error(`Invalid request: ${error.errorMessage}`);
      } else {
        throw new Error(`Server error: ${response.status}`);
      }
    }
    
    const data = await response.json();
    return data;
    
  } catch (error) {
    console.error('Error fetching audit logs:', error);
    // Show user-friendly error message
    alert(`Failed to load audit logs: ${error.message}`);
    return null;
  }
}
```

## Performance Tips

1. **Use Pagination**: Always specify `pageSize` to limit results (default 50, max 100)
2. **Filter Early**: Use specific filters to reduce data transfer
3. **Cache Results**: Cache audit data for 30-60 seconds to reduce API calls
4. **Batch Requests**: Combine multiple filters in single request
5. **Use Summary**: The `summary` object provides aggregated stats without parsing all entries

## Security Best Practices

1. **Authentication**: Always include ARC-0014 signed transaction in Authorization header
2. **HTTPS Only**: Never use HTTP in production
3. **Token Refresh**: Implement token refresh logic for long-running dashboards
4. **Rate Limiting**: Implement client-side rate limiting to avoid API throttling
5. **Error Handling**: Never expose sensitive errors to end users

## Support

- **API Documentation**: https://api.biatec.io/swagger
- **Technical Docs**: See `WHITELIST_ENFORCEMENT_COMPLIANCE_VERIFICATION.md`
- **Enterprise Audit Guide**: See `ENTERPRISE_AUDIT_API.md`
- **Support**: support@biatec.io

## Complete Working Example

See `example-dashboard.html` in the repository for a complete standalone HTML/JavaScript dashboard implementation that you can deploy immediately.

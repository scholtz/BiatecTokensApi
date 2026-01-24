# Network Compliance Metadata Endpoint

## Overview

The Network Compliance Metadata endpoint provides per-network compliance information for all supported blockchain networks in the BiatecTokensApi platform. This endpoint helps frontend applications display network-specific compliance indicators when users are deploying tokens.

## Endpoint

**GET** `/api/v1/compliance/networks`

Returns compliance metadata for all supported blockchain networks, including MICA readiness, whitelisting requirements, and regulatory specifications.

## Authentication

Required: ARC-0014 authentication (Algorand Request for Comments 14)

Authorization header format: `Authorization: SigTx <signed-transaction>`

## Response

**Success Response (200 OK):**

```json
{
  "success": true,
  "networks": [
    {
      "network": "voimain-v1.0",
      "networkName": "VOI Mainnet",
      "isMicaReady": true,
      "requiresWhitelisting": true,
      "requiresJurisdiction": true,
      "requiresRegulatoryFramework": false,
      "complianceRequirements": "VOI network requires jurisdiction specification for RWA token compliance tracking. Whitelisting is strongly recommended for enterprise tokens.",
      "source": "Network policy and MICA compliance guidelines",
      "lastUpdated": "2026-01-24T20:00:00Z"
    },
    {
      "network": "aramidmain-v1.0",
      "networkName": "Aramid Mainnet",
      "isMicaReady": true,
      "requiresWhitelisting": true,
      "requiresJurisdiction": false,
      "requiresRegulatoryFramework": true,
      "complianceRequirements": "Aramid network requires regulatory framework specification when compliance status is 'Compliant'. Whitelisting controls are mandatory for RWA tokens.",
      "source": "Network policy and MICA compliance guidelines",
      "lastUpdated": "2026-01-24T20:00:00Z"
    },
    {
      "network": "mainnet-v1.0",
      "networkName": "Algorand Mainnet",
      "isMicaReady": true,
      "requiresWhitelisting": false,
      "requiresJurisdiction": false,
      "requiresRegulatoryFramework": false,
      "complianceRequirements": "Algorand mainnet supports optional compliance features. MICA compliance can be achieved through metadata and whitelisting configuration.",
      "source": "Network policy",
      "lastUpdated": "2026-01-24T20:00:00Z"
    },
    {
      "network": "testnet-v1.0",
      "networkName": "Algorand Testnet",
      "isMicaReady": false,
      "requiresWhitelisting": false,
      "requiresJurisdiction": false,
      "requiresRegulatoryFramework": false,
      "complianceRequirements": "Testnet is for development and testing purposes only. Not suitable for production RWA tokens or MICA compliance.",
      "source": "Network policy",
      "lastUpdated": "2026-01-24T20:00:00Z"
    },
    {
      "network": "betanet-v1.0",
      "networkName": "Algorand Betanet",
      "isMicaReady": false,
      "requiresWhitelisting": false,
      "requiresJurisdiction": false,
      "requiresRegulatoryFramework": false,
      "complianceRequirements": "Betanet is for testing protocol upgrades. Not suitable for production use or MICA compliance.",
      "source": "Network policy",
      "lastUpdated": "2026-01-24T20:00:00Z"
    }
  ],
  "cacheDurationSeconds": 3600
}
```

**Error Response (500 Internal Server Error):**

```json
{
  "success": false,
  "errorMessage": "Failed to retrieve network compliance metadata: [error details]",
  "networks": [],
  "cacheDurationSeconds": 3600
}
```

## Response Fields

### NetworkComplianceMetadata

| Field | Type | Description |
|-------|------|-------------|
| network | string | Network identifier (e.g., "voimain-v1.0", "aramidmain-v1.0") |
| networkName | string | Human-readable network name (e.g., "VOI Mainnet") |
| isMicaReady | boolean | Indicates if the network supports MICA (Markets in Crypto-Assets) compliance |
| requiresWhitelisting | boolean | Indicates if whitelisting is required for RWA tokens on this network |
| requiresJurisdiction | boolean | Indicates if jurisdiction specification is required |
| requiresRegulatoryFramework | boolean | Indicates if regulatory framework specification is required |
| complianceRequirements | string | Detailed description of network-specific compliance requirements |
| source | string | Source of compliance metadata (e.g., "Network policy and MICA compliance guidelines") |
| lastUpdated | DateTime | Timestamp when metadata was last updated |

### NetworkComplianceMetadataResponse

| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Indicates if the request was successful |
| networks | array | List of NetworkComplianceMetadata objects |
| cacheDurationSeconds | integer | Recommended client-side cache duration in seconds (default: 3600) |
| errorMessage | string? | Error message if request failed |

## Supported Networks

### Production Networks (MICA-Ready)

1. **VOI Mainnet** (`voimain-v1.0`)
   - MICA Ready: ✅ Yes
   - Requires Whitelisting: ✅ Yes
   - Requires Jurisdiction: ✅ Yes
   - Requires Regulatory Framework: ❌ No

2. **Aramid Mainnet** (`aramidmain-v1.0`)
   - MICA Ready: ✅ Yes
   - Requires Whitelisting: ✅ Yes
   - Requires Jurisdiction: ❌ No
   - Requires Regulatory Framework: ✅ Yes

3. **Algorand Mainnet** (`mainnet-v1.0`)
   - MICA Ready: ✅ Yes (with optional features)
   - Requires Whitelisting: ❌ No (optional)
   - Requires Jurisdiction: ❌ No (optional)
   - Requires Regulatory Framework: ❌ No (optional)

### Test Networks (Not MICA-Ready)

4. **Algorand Testnet** (`testnet-v1.0`)
   - MICA Ready: ❌ No (development only)
   - For testing and development purposes

5. **Algorand Betanet** (`betanet-v1.0`)
   - MICA Ready: ❌ No (protocol testing)
   - For testing protocol upgrades

## Caching

The endpoint returns cache headers to optimize frontend performance:

```http
Cache-Control: public, max-age=3600
```

**Recommendations:**
- Cache responses for 1 hour (3600 seconds)
- Re-fetch when deploying a new token if network selection changes
- Consider using ETags for conditional requests

## Use Cases

### 1. Network Selection UI

Display compliance requirements when users select a network:

```javascript
// Fetch network compliance metadata
const response = await fetch('/api/v1/compliance/networks', {
  headers: {
    'Authorization': `SigTx ${signedTransaction}`
  }
});

const { networks } = await response.json();

// Display network options with compliance indicators
networks.forEach(network => {
  const option = document.createElement('option');
  option.value = network.network;
  option.textContent = `${network.networkName} ${network.isMicaReady ? '✅ MICA' : ''}`;
  option.dataset.requirements = network.complianceRequirements;
  networkSelect.appendChild(option);
});
```

### 2. Compliance Warning

Show warnings when selecting networks with strict requirements:

```javascript
function showComplianceWarning(selectedNetwork) {
  const network = networks.find(n => n.network === selectedNetwork);
  
  if (network.requiresWhitelisting) {
    showWarning('⚠️ This network requires whitelisting controls for RWA tokens');
  }
  
  if (network.requiresJurisdiction) {
    showWarning('⚠️ This network requires jurisdiction specification');
  }
  
  if (network.requiresRegulatoryFramework) {
    showWarning('⚠️ This network requires regulatory framework specification');
  }
}
```

### 3. MICA-Ready Filter

Filter and display only MICA-ready networks:

```javascript
const micaReadyNetworks = networks.filter(n => n.isMicaReady);

console.log('MICA-ready networks:', micaReadyNetworks.map(n => n.networkName));
// Output: ["VOI Mainnet", "Aramid Mainnet", "Algorand Mainnet"]
```

### 4. Compliance Dashboard

Display network compliance status in a dashboard:

```javascript
function renderComplianceDashboard(networks) {
  return networks.map(network => `
    <div class="network-card">
      <h3>${network.networkName}</h3>
      <div class="compliance-badges">
        ${network.isMicaReady ? '<span class="badge badge-success">MICA Ready</span>' : ''}
        ${network.requiresWhitelisting ? '<span class="badge badge-warning">Whitelisting Required</span>' : ''}
      </div>
      <p>${network.complianceRequirements}</p>
      <small>Source: ${network.source}</small>
    </div>
  `).join('');
}
```

## Error Handling

The endpoint returns errors in the following scenarios:

1. **Authentication Failure (401)**
   - Missing or invalid ARC-0014 authentication token
   - Response body: Standard 401 Unauthorized

2. **Internal Server Error (500)**
   - Service unavailable
   - Unexpected errors during data retrieval
   - Response includes error details in `errorMessage` field

Example error handling:

```javascript
try {
  const response = await fetch('/api/v1/compliance/networks', {
    headers: { 'Authorization': `SigTx ${signedTransaction}` }
  });
  
  if (!response.ok) {
    if (response.status === 401) {
      throw new Error('Authentication required. Please sign in.');
    }
    throw new Error(`HTTP ${response.status}: ${response.statusText}`);
  }
  
  const data = await response.json();
  
  if (!data.success) {
    throw new Error(data.errorMessage || 'Failed to load network metadata');
  }
  
  return data.networks;
} catch (error) {
  console.error('Error fetching network compliance metadata:', error);
  showError(error.message);
}
```

## Related Endpoints

This endpoint complements other compliance APIs:

- **`GET /api/v1/compliance/{assetId}`** - Full compliance metadata for a specific token
- **`GET /api/v1/token/{assetId}/compliance-indicators`** - Token-specific compliance indicators
- **`GET /api/v1/compliance/networks`** - Network compliance metadata ⭐ **NEW**

### When to Use Which Endpoint

| Use Case | Recommended Endpoint |
|----------|---------------------|
| Display network selection dropdown | `/compliance/networks` |
| Show compliance requirements per network | `/compliance/networks` |
| Filter MICA-ready networks | `/compliance/networks` |
| Check specific token compliance | `/token/{assetId}/compliance-indicators` |
| Full token compliance details | `/compliance/{assetId}` |

## Performance Considerations

- **Response Time**: < 10ms (in-memory data, no database queries)
- **Response Size**: ~2KB (5 networks with metadata)
- **Rate Limiting**: Subject to standard API rate limits
- **Caching**: 
  - Server-side: Data is hardcoded and doesn't change frequently
  - Client-side: Cache for 1 hour as indicated by `cacheDurationSeconds`

## Security

- **Authentication Required**: All requests must include valid ARC-0014 authentication
- **Public Data**: Network compliance metadata is not sensitive and can be safely cached
- **Audit Logging**: All requests are logged for compliance audit trails
- **No User-Specific Data**: Response is same for all authenticated users

## Example Requests

### cURL

```bash
curl -X GET "https://api.biatec.io/api/v1/compliance/networks" \
  -H "Authorization: SigTx <signed-transaction>" \
  -H "Accept: application/json"
```

### JavaScript (Fetch)

```javascript
const response = await fetch('https://api.biatec.io/api/v1/compliance/networks', {
  method: 'GET',
  headers: {
    'Authorization': `SigTx ${signedTransaction}`,
    'Accept': 'application/json'
  }
});

const data = await response.json();
console.log('Networks:', data.networks);
```

### Python

```python
import requests

response = requests.get(
    'https://api.biatec.io/api/v1/compliance/networks',
    headers={
        'Authorization': f'SigTx {signed_transaction}',
        'Accept': 'application/json'
    }
)

data = response.json()
print('Networks:', data['networks'])
```

## Support

For questions or issues:
1. Review comprehensive test suite: `BiatecTokensTests/NetworkComplianceMetadataTests.cs`
2. Check implementation: `BiatecTokensApi/Services/ComplianceService.cs`
3. Review controller: `BiatecTokensApi/Controllers/ComplianceController.cs`
4. Consult API documentation at `/swagger`

---

**Last Updated**: 2026-01-24  
**API Version**: v1  
**Endpoint**: `/api/v1/compliance/networks`

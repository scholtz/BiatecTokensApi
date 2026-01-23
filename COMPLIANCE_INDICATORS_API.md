# Token Compliance Indicators API

## Overview

The Token Compliance Indicators API provides a simplified, frontend-friendly view of compliance and whitelisting status for tokens deployed through the BiatecTokensApi platform. This endpoint enables the frontend to surface enterprise readiness indicators and regulatory compliance status to support subscription value and compliance dashboards.

## Endpoint

### Get Token Compliance Indicators

**GET** `/api/v1/token/{assetId}/compliance-indicators`

Retrieves compliance indicators for a specific token, aggregating data from:
- Compliance metadata (if configured)
- Whitelist entries
- Regulatory framework information
- KYC verification status

#### Authentication

Required: ARC-0014 authentication (Algorand Request for Comments 14)

#### Path Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| assetId | ulong | Yes | The asset ID (token ID) to get compliance indicators for |

#### Response

**Success Response (200 OK):**

```json
{
  "success": true,
  "indicators": {
    "assetId": 12345,
    "isMicaReady": true,
    "whitelistingEnabled": true,
    "whitelistedAddressCount": 50,
    "hasTransferRestrictions": true,
    "transferRestrictions": "KYC required for all transfers",
    "requiresAccreditedInvestors": true,
    "complianceStatus": "Compliant",
    "verificationStatus": "Verified",
    "regulatoryFramework": "MICA",
    "jurisdiction": "EU",
    "maxHolders": 100,
    "enterpriseReadinessScore": 100,
    "network": "voimain-v1.0",
    "hasComplianceMetadata": true,
    "lastComplianceUpdate": "2026-01-23T12:00:00Z"
  }
}
```

**Error Response (500 Internal Server Error):**

```json
{
  "success": false,
  "errorMessage": "Failed to retrieve compliance indicators: [error details]"
}
```

## Response Fields

### TokenComplianceIndicators

| Field | Type | Description |
|-------|------|-------------|
| assetId | ulong | The asset ID (token ID) |
| isMicaReady | boolean | Indicates if the token meets MICA regulatory requirements |
| whitelistingEnabled | boolean | Indicates if whitelisting controls are enabled |
| whitelistedAddressCount | integer | Number of addresses currently whitelisted |
| hasTransferRestrictions | boolean | Indicates if transfer restrictions are in place |
| transferRestrictions | string? | Description of transfer restrictions (if any) |
| requiresAccreditedInvestors | boolean | Whether token requires accredited investors only |
| complianceStatus | string? | Current compliance status (Compliant, NonCompliant, UnderReview, etc.) |
| verificationStatus | string? | KYC verification status (Verified, Pending, Failed, etc.) |
| regulatoryFramework | string? | Applicable regulatory framework(s) (e.g., "MICA", "SEC Reg D") |
| jurisdiction | string? | Jurisdiction(s) where token is compliant (ISO country codes) |
| maxHolders | integer? | Maximum number of token holders allowed |
| enterpriseReadinessScore | integer | Overall enterprise readiness score (0-100) |
| network | string? | Network on which the token is deployed |
| hasComplianceMetadata | boolean | Whether compliance metadata exists for this token |
| lastComplianceUpdate | DateTime? | Date when compliance metadata was last updated |

## MICA Readiness Criteria

A token is considered MICA-ready when **all** of the following conditions are met:

1. **Compliance metadata exists** for the token
2. **Compliance status** is either:
   - `Compliant` - Token meets regulatory requirements
   - `Exempt` - Token is exempt from certain regulations
3. **Regulatory framework** is specified (e.g., "MICA")
4. **Jurisdiction** is specified (e.g., "EU")

## Enterprise Readiness Score

The Enterprise Readiness Score (0-100) is calculated based on the following factors:

| Factor | Points | Description |
|--------|--------|-------------|
| Compliance Metadata | 30 | Token has compliance metadata configured |
| Whitelist Controls | 25 | Token has whitelist controls enabled with entries |
| KYC Verification | 20 | KYC verification status is `Verified` |
| Regulatory Framework | 15 | Regulatory framework is specified |
| Jurisdiction | 10 | Jurisdiction is specified |
| **Total** | **100** | |

### Score Interpretation

- **80-100**: Enterprise-ready - Full compliance infrastructure in place
- **60-79**: Advanced compliance - Most features configured, minor gaps
- **40-59**: Basic compliance - Some features configured, significant gaps
- **20-39**: Minimal compliance - Very few features configured
- **0-19**: Not compliant - Little to no compliance infrastructure

## Use Cases

### Frontend Dashboard

Display enterprise readiness indicators on token management dashboard:

```javascript
// Example: Fetch and display compliance indicators
const response = await fetch(`/api/v1/token/${assetId}/compliance-indicators`, {
  headers: {
    'Authorization': `SigTx ${signedTransaction}`
  }
});

const { indicators } = await response.json();

// Display MICA readiness badge
if (indicators.isMicaReady) {
  showBadge('MICA Ready', 'success');
}

// Display enterprise readiness score
showScore(indicators.enterpriseReadinessScore);

// Display whitelist status
if (indicators.whitelistingEnabled) {
  showInfo(`${indicators.whitelistedAddressCount} whitelisted addresses`);
}
```

### Subscription Value Features

Highlight premium compliance features to drive subscription upgrades:

```javascript
if (!indicators.hasComplianceMetadata) {
  showUpgradePrompt('Add compliance metadata to increase enterprise readiness');
}

if (indicators.enterpriseReadinessScore < 60) {
  showUpgradePrompt('Upgrade to Enterprise tier for full compliance features');
}
```

### Compliance Reporting

Generate compliance reports using indicators:

```javascript
const tokens = await getTokenList();
const complianceReport = await Promise.all(
  tokens.map(async (token) => {
    const { indicators } = await getComplianceIndicators(token.assetId);
    return {
      assetId: token.assetId,
      name: token.name,
      isMicaReady: indicators.isMicaReady,
      score: indicators.enterpriseReadinessScore,
      status: indicators.complianceStatus
    };
  })
);

// Filter by MICA-ready tokens
const micaReadyTokens = complianceReport.filter(t => t.isMicaReady);
```

## Example Responses

### Fully Compliant Token

```json
{
  "success": true,
  "indicators": {
    "assetId": 12345,
    "isMicaReady": true,
    "whitelistingEnabled": true,
    "whitelistedAddressCount": 150,
    "hasTransferRestrictions": true,
    "transferRestrictions": "KYC required; accredited investors only",
    "requiresAccreditedInvestors": true,
    "complianceStatus": "Compliant",
    "verificationStatus": "Verified",
    "regulatoryFramework": "MICA, SEC Reg D",
    "jurisdiction": "EU, US",
    "maxHolders": 100,
    "enterpriseReadinessScore": 100,
    "network": "voimain-v1.0",
    "hasComplianceMetadata": true,
    "lastComplianceUpdate": "2026-01-23T12:00:00Z"
  }
}
```

### Basic Token (No Compliance)

```json
{
  "success": true,
  "indicators": {
    "assetId": 67890,
    "isMicaReady": false,
    "whitelistingEnabled": false,
    "whitelistedAddressCount": 0,
    "hasTransferRestrictions": false,
    "transferRestrictions": null,
    "requiresAccreditedInvestors": false,
    "complianceStatus": null,
    "verificationStatus": null,
    "regulatoryFramework": null,
    "jurisdiction": null,
    "maxHolders": null,
    "enterpriseReadinessScore": 0,
    "network": null,
    "hasComplianceMetadata": false,
    "lastComplianceUpdate": null
  }
}
```

### Partially Compliant Token

```json
{
  "success": true,
  "indicators": {
    "assetId": 54321,
    "isMicaReady": false,
    "whitelistingEnabled": true,
    "whitelistedAddressCount": 25,
    "hasTransferRestrictions": false,
    "transferRestrictions": null,
    "requiresAccreditedInvestors": false,
    "complianceStatus": "UnderReview",
    "verificationStatus": "Pending",
    "regulatoryFramework": "MICA",
    "jurisdiction": null,
    "maxHolders": null,
    "enterpriseReadinessScore": 70,
    "network": "voimain-v1.0",
    "hasComplianceMetadata": true,
    "lastComplianceUpdate": "2026-01-20T08:30:00Z"
  }
}
```

## Integration with Existing APIs

This endpoint complements existing compliance APIs:

- **`GET /api/v1/compliance/{assetId}`** - Full compliance metadata (detailed)
- **`GET /api/v1/whitelist/{assetId}`** - Whitelist entries (detailed)
- **`GET /api/v1/token/{assetId}/compliance-indicators`** - Compliance indicators (summary) ⭐ **NEW**

### When to Use Which Endpoint

| Use Case | Recommended Endpoint |
|----------|---------------------|
| Dashboard badge/indicator | `/token/{assetId}/compliance-indicators` |
| Quick status check | `/token/{assetId}/compliance-indicators` |
| Enterprise readiness score | `/token/{assetId}/compliance-indicators` |
| Full compliance details | `/compliance/{assetId}` |
| Managing compliance metadata | `/compliance` (POST/PUT/DELETE) |
| Managing whitelist entries | `/whitelist` (POST/DELETE) |

## Error Handling

The endpoint returns errors in the following scenarios:

1. **Authentication Failure (401)**
   - Missing or invalid ARC-0014 authentication token

2. **Internal Server Error (500)**
   - Database connection issues
   - Service unavailable
   - Unexpected errors during data aggregation

```json
{
  "success": false,
  "errorMessage": "Failed to retrieve compliance indicators: Database connection timeout"
}
```

## Performance Considerations

- **Response Time**: Typically < 100ms for cached metadata
- **Rate Limiting**: Subject to standard API rate limits
- **Caching**: Consider caching indicators on the frontend for 5-10 minutes
- **Batch Requests**: For multiple tokens, make parallel requests (max 10 concurrent)

## Security

- **Authentication Required**: All requests must include valid ARC-0014 authentication
- **Data Privacy**: Only returns metadata for tokens the authenticated user has permission to view
- **Audit Logging**: All requests are logged for compliance audit trails

## Related Documentation

- [Compliance API Documentation](./COMPLIANCE_API.md)
- [Whitelist Feature Documentation](./WHITELIST_FEATURE.md)
- [MICA Compliance Guide](./VOI_ARAMID_COMPLIANCE_IMPLEMENTATION.md)
- [Subscription Tier Gating](./SUBSCRIPTION_TIER_GATING.md)

## Support

For questions or issues with the compliance indicators API, please:
1. Check the comprehensive test suite in `BiatecTokensTests/TokenComplianceIndicatorsTests.cs`
2. Review the service implementation in `BiatecTokensApi/Services/ComplianceService.cs`
3. Consult the controller implementation in `BiatecTokensApi/Controllers/TokenController.cs`

---

**Last Updated**: 2026-01-23
**API Version**: v1

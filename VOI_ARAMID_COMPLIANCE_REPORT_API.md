# VOI/Aramid Token Compliance Report API

## Overview
This document describes the VOI/Aramid token compliance report API endpoint. This endpoint provides enterprise-grade compliance reporting for VOI and Aramid networks, aggregating compliance metadata, whitelist statistics, and audit logs to support MICA dashboard requirements and regulatory reporting.

## Endpoint

### GET /api/v1/compliance/report

**Description:** Generates a comprehensive compliance report for VOI/Aramid tokens with filtering options.

**Authentication:** Required (ARC-0014)

**Query Parameters:**
- `assetId` (optional): Filter by specific asset ID
- `network` (optional): Filter by network (voimain-v1.0, aramidmain-v1.0)
- `fromDate` (optional): Start date filter for audit events (ISO 8601 format)
- `toDate` (optional): End date filter for audit events (ISO 8601 format)
- `includeWhitelistDetails` (optional, default: true): Include detailed whitelist information
- `includeTransferAudits` (optional, default: true): Include recent transfer validation audit events
- `includeComplianceAudits` (optional, default: true): Include compliance metadata changes audit log
- `maxAuditEntriesPerCategory` (optional, default: 100, max: 1000): Maximum number of audit entries per category
- `page` (optional, default: 1): Page number for pagination
- `pageSize` (optional, default: 50, max: 100): Page size for pagination

**Response:** `TokenComplianceReportResponse`

```json
{
  "success": true,
  "tokens": [
    {
      "assetId": 12345,
      "network": "voimain-v1.0",
      "complianceMetadata": {
        "id": "guid",
        "assetId": 12345,
        "kycProvider": "Sumsub",
        "kycVerificationDate": "2026-01-21T00:00:00Z",
        "verificationStatus": "Verified",
        "jurisdiction": "US,EU",
        "regulatoryFramework": "SEC Reg D, MiFID II",
        "complianceStatus": "Compliant",
        "lastComplianceReview": "2026-01-15T00:00:00Z",
        "nextComplianceReview": "2026-07-15T00:00:00Z",
        "assetType": "Security Token",
        "transferRestrictions": "Accredited investors only",
        "maxHolders": 500,
        "requiresAccreditedInvestors": true,
        "network": "voimain-v1.0",
        "notes": "Additional compliance notes",
        "createdBy": "ALGORAND_ADDRESS",
        "createdAt": "2026-01-01T00:00:00Z",
        "updatedAt": "2026-01-21T00:00:00Z",
        "updatedBy": "ALGORAND_ADDRESS"
      },
      "whitelistSummary": {
        "totalAddresses": 150,
        "activeAddresses": 145,
        "revokedAddresses": 3,
        "suspendedAddresses": 2,
        "kycVerifiedAddresses": 140,
        "lastModified": "2026-01-22T00:00:00Z",
        "transferValidationsCount": 523,
        "deniedTransfersCount": 8
      },
      "complianceAuditEntries": [
        {
          "id": "audit-guid-1",
          "assetId": 12345,
          "network": "voimain-v1.0",
          "actionType": "Update",
          "performedBy": "ADMIN_ADDRESS",
          "performedAt": "2026-01-21T10:30:00Z",
          "success": true,
          "oldComplianceStatus": "UnderReview",
          "newComplianceStatus": "Compliant",
          "notes": "Completed compliance review"
        }
      ],
      "whitelistAuditEntries": [
        {
          "id": "whitelist-audit-guid-1",
          "assetId": 12345,
          "address": "ADDRESS1...",
          "actionType": "Add",
          "performedBy": "ADMIN_ADDRESS",
          "performedAt": "2026-01-20T15:00:00Z",
          "newStatus": "Active",
          "network": "voimain-v1.0",
          "role": "Admin"
        }
      ],
      "transferValidationEntries": [
        {
          "id": "transfer-audit-guid-1",
          "assetId": 12345,
          "address": "SENDER_ADDRESS",
          "actionType": "TransferValidation",
          "performedBy": "VALIDATOR_ADDRESS",
          "performedAt": "2026-01-22T08:15:00Z",
          "toAddress": "RECEIVER_ADDRESS",
          "transferAllowed": true,
          "amount": 1000000,
          "network": "voimain-v1.0"
        }
      ],
      "complianceHealthScore": 95,
      "warnings": [],
      "networkSpecificStatus": {
        "meetsNetworkRequirements": true,
        "satisfiedRules": [
          "VOI: KYC verification present for accredited investor tokens",
          "VOI: Jurisdiction specified for compliance tracking"
        ],
        "violatedRules": [],
        "recommendations": []
      }
    }
  ],
  "totalCount": 1,
  "page": 1,
  "pageSize": 50,
  "totalPages": 1,
  "generatedAt": "2026-01-23T09:45:00Z",
  "networkFilter": "voimain-v1.0",
  "subscriptionInfo": {
    "tierName": "Enterprise",
    "auditLogEnabled": true,
    "maxAssetsPerReport": 100,
    "detailedReportsEnabled": true,
    "limitationMessage": null,
    "metered": true
  }
}
```

**Status Codes:**
- `200 OK`: Success
- `400 Bad Request`: Invalid request parameters
- `401 Unauthorized`: Missing or invalid authentication
- `500 Internal Server Error`: Server error

---

## Report Components

### Compliance Metadata
Contains all compliance-related information for the token:
- KYC provider and verification status
- Regulatory framework and jurisdiction
- Compliance status and review dates
- Asset type and transfer restrictions
- Max holders and accredited investor requirements

### Whitelist Summary
Statistical summary of the token's whitelist:
- Total, active, revoked, and suspended addresses
- Number of KYC-verified addresses
- Transfer validation statistics
- Last modification timestamp

### Compliance Audit Entries
Recent compliance metadata changes:
- Metadata updates (compliance status, verification status changes)
- Who made the changes and when
- Old and new values for tracking changes

### Whitelist Audit Entries
Recent whitelist modifications:
- Address additions, updates, and removals
- Status changes and role information
- Operator and admin actions

### Transfer Validation Entries
Recent transfer validation events:
- Successful and denied transfers
- Sender, receiver, and amount information
- Denial reasons (if applicable)
- Network information

### Compliance Health Score
A calculated score (0-100) based on multiple factors:
- **40 points** - Compliance Status (Compliant = 40, UnderReview = 20, Exempt = 30, NonCompliant/Suspended = 0)
- **30 points** - Verification Status (Verified = 30, InProgress = 15, Pending = 10, Failed/Expired = 0)
- **10 points** - Regulatory Framework specified
- **10 points** - KYC Provider specified
- **10 points** - Jurisdiction specified

### Network-Specific Compliance Status
Evaluates compliance against VOI/Aramid network-specific rules:

**VOI Network Rules:**
1. KYC verification recommended for tokens requiring accredited investors
2. Jurisdiction should be specified

**Aramid Network Rules:**
1. Regulatory framework required for compliant status
2. MaxHolders should be set for security tokens

### Warnings
Automated compliance warnings based on token status:
- Expired KYC verification
- Failed KYC verification
- Overdue compliance review
- Non-compliant or suspended status
- Network requirement violations

---

## Usage Examples

### Example 1: VOI Network Compliance Report
Get a compliance report for all VOI network tokens:

```bash
GET /api/v1/compliance/report?network=voimain-v1.0
Authorization: SigTx <signed-transaction>
```

### Example 2: Aramid Network Report for Specific Token
Get a detailed report for a specific token on Aramid network:

```bash
GET /api/v1/compliance/report?assetId=12345&network=aramidmain-v1.0&includeWhitelistDetails=true&includeTransferAudits=true
Authorization: SigTx <signed-transaction>
```

### Example 3: Date Range Filtered Report
Get compliance report for VOI tokens with audit events from a specific period:

```bash
GET /api/v1/compliance/report?network=voimain-v1.0&fromDate=2026-01-01T00:00:00Z&toDate=2026-01-31T23:59:59Z
Authorization: SigTx <signed-transaction>
```

### Example 4: Minimal Report (No Audit Details)
Get a lightweight report with only compliance metadata and whitelist summary:

```bash
GET /api/v1/compliance/report?network=voimain-v1.0&includeComplianceAudits=false&includeTransferAudits=false
Authorization: SigTx <signed-transaction>
```

---

## Subscription Tiers

The compliance report endpoint is designed for paid subscription tiers:

| Tier | Audit Log Access | Max Assets/Report | Detailed Reports | Metered |
|------|------------------|-------------------|------------------|---------|
| Free | No | N/A | No | No |
| Basic | Yes | 10 | Yes | Yes |
| Premium | Yes | 50 | Yes | Yes |
| Enterprise | Yes | 100 | Yes | Yes |

**Note:** Each report generation emits a metering event for billing analytics.

---

## Metering

This endpoint emits a metering event with the following information:
- **Category**: Compliance
- **OperationType**: Upsert (generic operation)
- **Network**: Requested network filter
- **PerformedBy**: Authenticated user
- **ItemCount**: Number of tokens in the report
- **Metadata**: Report configuration (includeWhitelist, includeTransfers, includeCompliance)

---

## Use Cases

### Enterprise Dashboards
Use this endpoint to populate compliance dashboards with:
- Real-time compliance status across all tokens
- Network-specific compliance tracking (VOI/Aramid)
- Audit trail visualization
- Warning and recommendation displays

### MICA Regulatory Reporting
Generate comprehensive compliance reports for MICA requirements:
- KYC verification tracking
- Jurisdiction and regulatory framework compliance
- Transfer restriction enforcement
- Audit log retention (7 years minimum)

### Compliance Monitoring
Automated compliance monitoring and alerting:
- Detect expired KYC verifications
- Identify overdue compliance reviews
- Monitor network requirement violations
- Track transfer validation patterns

### Incident Investigation
Use detailed audit logs for incident response:
- Filter by date range for specific incidents
- Track who made changes and when
- Review transfer denial patterns
- Analyze compliance status changes

---

## Best Practices

1. **Filter by Network**: Always specify network filter (voimain-v1.0 or aramidmain-v1.0) for targeted reporting
2. **Pagination**: Use appropriate page size based on your needs (default 50, max 100)
3. **Date Ranges**: Use date filters to limit audit log entries and improve performance
4. **Selective Inclusion**: Disable audit details you don't need to reduce response size
5. **Health Score Monitoring**: Monitor compliance health scores and investigate scores below 70
6. **Warning Review**: Regularly review warnings and take corrective actions
7. **Subscription Planning**: Choose appropriate tier based on number of assets you need to monitor

---

## Error Handling

### Common Error Scenarios

**Missing Authentication:**
```json
{
  "success": false,
  "errorMessage": "User address not found in authentication context"
}
```

**Invalid Parameters:**
```json
{
  "success": false,
  "errorMessage": "Invalid date range: fromDate must be before toDate"
}
```

**Internal Error:**
```json
{
  "success": false,
  "errorMessage": "Internal error: Database connection failed"
}
```

---

## Performance Considerations

- **Response Size**: Reports with full audit details can be large (5-10 MB for 100 tokens with full audit history)
- **Query Time**: Expect 200-500ms for typical reports, longer for large date ranges
- **Caching**: Consider caching reports for frequently accessed tokens
- **Rate Limiting**: Respect rate limits to avoid service degradation

---

## Related Endpoints

- `GET /api/v1/compliance/{assetId}` - Get compliance metadata for a specific token
- `GET /api/v1/compliance/audit-log` - Get compliance audit log with filtering
- `GET /api/v1/whitelist/audit-log` - Get whitelist audit log with filtering
- `POST /api/v1/compliance` - Create or update compliance metadata

---

## Changelog

### Version 1.0 (Initial Release)
- Initial release of VOI/Aramid compliance report endpoint
- Support for network-specific filtering
- Compliance health score calculation
- Network-specific rule evaluation
- Subscription tier integration
- Metering for billing analytics

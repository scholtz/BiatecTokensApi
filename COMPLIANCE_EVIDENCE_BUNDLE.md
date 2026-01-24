# Compliance Evidence Bundle Export API

## Overview

The Compliance Evidence Bundle Export API provides auditor-ready ZIP bundles containing comprehensive compliance evidence for MICA (Markets in Crypto-Assets Regulation) and RWA (Real World Assets) compliance audits.

**Implementation Date:** January 24, 2026

## Features

### Core Capabilities

✅ **Comprehensive Evidence Collection**: Includes audit logs, whitelist history, transfer approvals, and policy metadata  
✅ **Cryptographic Verification**: SHA256 checksums for all files and the entire bundle  
✅ **Timestamped Exports**: UTC timestamps for audit trail verification  
✅ **Manifest Generation**: Detailed manifest with file metadata and checksums  
✅ **Human-Readable README**: Included documentation for auditors  
✅ **MICA/RWA Compliance**: Aligned with 7-year retention requirements  
✅ **Network-Specific Data**: Supports VOI, Aramid, and all Algorand networks  
✅ **Flexible Filtering**: Date range and content type filtering  
✅ **Access Control**: ARC-0014 authentication required  
✅ **Audit Trail**: Every export is logged for compliance tracking  

## API Endpoint

### Generate Compliance Evidence Bundle

**Endpoint:** `POST /api/v1/compliance/evidence-bundle`

**Authentication:** Required (ARC-0014)

**Content-Type:** `application/json`

**Request Body:**
```json
{
  "assetId": 12345,
  "fromDate": "2024-01-01T00:00:00Z",
  "toDate": "2026-01-24T00:00:00Z",
  "includeWhitelistHistory": true,
  "includeTransferApprovals": true,
  "includeAuditLogs": true,
  "includePolicyMetadata": true,
  "includeTokenMetadata": true
}
```

**Request Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| assetId | ulong | Yes | Asset ID (token ID) for which to generate the bundle |
| fromDate | DateTime? | No | Start date for audit log filtering (ISO 8601 format) |
| toDate | DateTime? | No | End date for audit log filtering (ISO 8601 format) |
| includeWhitelistHistory | bool | No | Include whitelist entries and audit log (default: true) |
| includeTransferApprovals | bool | No | Include transfer validation records (default: true) |
| includeAuditLogs | bool | No | Include compliance audit logs (default: true) |
| includePolicyMetadata | bool | No | Include 7-year retention policy (default: true) |
| includeTokenMetadata | bool | No | Include token compliance metadata (default: true) |

**Response:**
- **Content-Type**: `application/zip`
- **Filename**: `compliance-evidence-{assetId}-{timestamp}.zip`
- **Status Codes**:
  - `200 OK`: Bundle generated successfully
  - `400 Bad Request`: Invalid request parameters
  - `401 Unauthorized`: Authentication required
  - `500 Internal Server Error`: Bundle generation failed

**Example cURL:**
```bash
curl -X POST https://api.example.com/api/v1/compliance/evidence-bundle \
  -H "Authorization: SigTx <signed-transaction>" \
  -H "Content-Type: application/json" \
  -d '{
    "assetId": 12345,
    "fromDate": "2024-01-01T00:00:00Z",
    "toDate": "2026-01-24T00:00:00Z"
  }' \
  --output compliance-evidence-12345.zip
```

## Bundle Structure

The generated ZIP file contains the following structure:

```
compliance-evidence-{assetId}-{timestamp}.zip
├── manifest.json                              # Bundle manifest with checksums
├── README.txt                                 # Human-readable documentation
├── metadata/
│   └── compliance_metadata.json              # Token compliance metadata
├── whitelist/
│   ├── current_entries.json                  # Current whitelist entries
│   └── audit_log.json                        # Whitelist operation history
├── audit_logs/
│   ├── compliance_operations.json            # Compliance metadata operations
│   └── transfer_validations.json             # Transfer validation records
└── policy/
    └── retention_policy.json                 # 7-year MICA retention policy
```

## Manifest Format

The `manifest.json` file contains comprehensive metadata about the bundle:

```json
{
  "bundleId": "a1b2c3d4e5f6...",
  "assetId": 12345,
  "generatedAt": "2026-01-24T09:30:00.000Z",
  "generatedBy": "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA",
  "fromDate": "2024-01-01T00:00:00.000Z",
  "toDate": "2026-01-24T00:00:00.000Z",
  "network": "voimain-v1.0",
  "complianceFramework": "MICA 2024",
  "retentionPeriodYears": 7,
  "bundleSha256": "a1b2c3d4e5f6...",
  "files": [
    {
      "path": "metadata/compliance_metadata.json",
      "description": "Token compliance metadata including KYC status...",
      "sha256": "abc123...",
      "sizeBytes": 1234,
      "format": "JSON"
    }
  ],
  "summary": {
    "auditLogCount": 145,
    "whitelistEntriesCount": 23,
    "whitelistRuleAuditCount": 67,
    "transferValidationCount": 89,
    "oldestRecordDate": "2024-01-15T08:30:00.000Z",
    "newestRecordDate": "2026-01-24T09:15:00.000Z",
    "hasComplianceMetadata": true,
    "hasTokenMetadata": true,
    "includedCategories": ["Create", "Update", "TransferValidation"]
  }
}
```

## File Contents

### 1. compliance_metadata.json

Contains token compliance metadata:
```json
{
  "assetId": 12345,
  "kycProvider": "Sumsub",
  "kycVerificationDate": "2024-06-15T00:00:00Z",
  "verificationStatus": "Verified",
  "jurisdiction": "US,EU",
  "regulatoryFramework": "SEC Reg D, MiFID II",
  "complianceStatus": "Compliant",
  "network": "voimain-v1.0"
}
```

### 2. current_entries.json

Current whitelist entries:
```json
[
  {
    "assetId": 12345,
    "address": "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA",
    "status": "Active",
    "createdAt": "2024-03-10T12:00:00Z",
    "createdBy": "ADMIN_ADDRESS...",
    "network": "voimain-v1.0"
  }
]
```

### 3. audit_log.json (whitelist)

Complete whitelist operation history:
```json
[
  {
    "id": "guid-123",
    "assetId": 12345,
    "address": "USER_ADDRESS...",
    "actionType": "Add",
    "performedBy": "ADMIN_ADDRESS...",
    "performedAt": "2024-03-10T12:00:00Z",
    "newStatus": "Active",
    "network": "voimain-v1.0"
  }
]
```

### 4. compliance_operations.json

Compliance metadata operation logs:
```json
[
  {
    "id": "guid-456",
    "assetId": 12345,
    "network": "voimain-v1.0",
    "actionType": "Create",
    "performedBy": "ISSUER_ADDRESS...",
    "performedAt": "2024-02-01T10:00:00Z",
    "success": true,
    "newComplianceStatus": "UnderReview"
  }
]
```

### 5. transfer_validations.json

Transfer validation records:
```json
[
  {
    "id": "guid-789",
    "assetId": 12345,
    "address": "SENDER_ADDRESS...",
    "actionType": "ValidateTransfer",
    "performedBy": "VALIDATOR_ADDRESS...",
    "performedAt": "2024-05-15T14:30:00Z",
    "toAddress": "RECEIVER_ADDRESS...",
    "transferAllowed": true,
    "network": "voimain-v1.0"
  }
]
```

### 6. retention_policy.json

7-year MICA retention policy:
```json
{
  "framework": "MICA 2024",
  "minimumRetentionPeriodYears": 7,
  "dataImmutability": "All audit entries are append-only...",
  "scope": "Whitelist, blacklist, compliance metadata...",
  "networkSupport": "All supported networks including VOI and Aramid..."
}
```

## Verification

### Bundle Integrity

1. **Verify Bundle Checksum:**
   ```bash
   sha256sum compliance-evidence-12345.zip
   ```
   Compare with `bundleSha256` in manifest.json

2. **Verify Individual Files:**
   ```bash
   unzip compliance-evidence-12345.zip
   sha256sum metadata/compliance_metadata.json
   ```
   Compare with corresponding entry in manifest.json

3. **Verify Manifest Contents:**
   - Check that all files listed in manifest are present
   - Verify file sizes match
   - Confirm timestamps are within expected range

### Audit Trail

Every bundle export is logged with:
- Bundle ID for tracking
- Asset ID
- Requester's address
- Generation timestamp
- Number of files included
- Bundle size

Query the audit log:
```
GET /api/v1/enterprise-audit/export?category=Compliance&actionType=Export
```

## Use Cases

### 1. MICA Compliance Audit

Generate a complete evidence bundle for regulatory review:
```bash
POST /api/v1/compliance/evidence-bundle
{
  "assetId": 12345,
  "fromDate": "2024-01-01T00:00:00Z",
  "toDate": "2026-01-01T00:00:00Z"
}
```

### 2. Procurement Compliance Evidence

Export evidence for a specific time period:
```bash
POST /api/v1/compliance/evidence-bundle
{
  "assetId": 12345,
  "fromDate": "2025-01-01T00:00:00Z",
  "toDate": "2025-12-31T23:59:59Z"
}
```

### 3. Selective Export

Export only specific evidence types:
```bash
POST /api/v1/compliance/evidence-bundle
{
  "assetId": 12345,
  "includeWhitelistHistory": true,
  "includeTransferApprovals": false,
  "includeAuditLogs": true,
  "includePolicyMetadata": true,
  "includeTokenMetadata": false
}
```

## Access Control

### Authorization

- **Required**: ARC-0014 Algorand authentication
- **Realm**: `BiatecTokens#ARC14`
- **Authorization Header**: `Authorization: SigTx <signed-transaction>`

### Recommended Roles

- Compliance Officers
- Internal Auditors
- External Auditors (with delegated access)
- Risk Management Team
- Legal Team

### Audit Logging

Every export generates:
- Audit log entry (ComplianceActionType.Export)
- Metering event for subscription tracking
- Bundle metadata for future reference

## Security

### Data Protection

- All data is sourced from immutable append-only logs
- No modification or deletion of source data
- Bundle generation does not affect operational data
- Checksums ensure data integrity

### Access Tracking

- Every export is logged with requester identity
- Bundle ID enables tracing of all exports
- Timestamp provides temporal context
- Network information included for multi-chain environments

### Best Practices

1. **Store Bundles Securely**: Use encrypted storage for exported bundles
2. **Regular Exports**: Schedule periodic exports for backup purposes
3. **Verify Checksums**: Always verify bundle and file checksums
4. **Retention**: Maintain exports for at least 7 years per MICA
5. **Access Control**: Limit export access to authorized personnel only

## Limitations

- Maximum 10,000 audit log entries per category
- Bundle generation is synchronous (may take several seconds)
- Date filters apply to audit logs only
- Current whitelist snapshot includes all entries regardless of date filter

## Integration

### Swagger/OpenAPI

The endpoint is fully documented in the Swagger UI:
```
https://your-api-domain/swagger
```

### Code Example (C#)

```csharp
using var httpClient = new HttpClient();
httpClient.DefaultRequestHeaders.Add("Authorization", "SigTx <your-signed-transaction>");

var request = new
{
    assetId = 12345,
    fromDate = "2024-01-01T00:00:00Z",
    toDate = "2026-01-24T00:00:00Z"
};

var response = await httpClient.PostAsJsonAsync(
    "https://api.example.com/api/v1/compliance/evidence-bundle",
    request
);

if (response.IsSuccessStatusCode)
{
    var zipBytes = await response.Content.ReadAsByteArrayAsync();
    await File.WriteAllBytesAsync("evidence-bundle.zip", zipBytes);
}
```

### Code Example (Python)

```python
import requests
import json

headers = {
    'Authorization': 'SigTx <your-signed-transaction>',
    'Content-Type': 'application/json'
}

data = {
    'assetId': 12345,
    'fromDate': '2024-01-01T00:00:00Z',
    'toDate': '2026-01-24T00:00:00Z'
}

response = requests.post(
    'https://api.example.com/api/v1/compliance/evidence-bundle',
    headers=headers,
    data=json.dumps(data)
)

if response.status_code == 200:
    with open('evidence-bundle.zip', 'wb') as f:
        f.write(response.content)
```

## Support

For questions, issues, or feature requests:
- Review the [API Documentation](../README.md)
- Check [MICA Compliance Documentation](./COMPLIANCE_API.md)
- Review [Enterprise Audit API Documentation](./ENTERPRISE_AUDIT_API.md)
- Contact the compliance team

## Changelog

### Version 1.0 (January 24, 2026)
- Initial release
- Support for all compliance evidence types
- SHA256 checksums for integrity verification
- MICA 2024 framework alignment
- VOI/Aramid network support

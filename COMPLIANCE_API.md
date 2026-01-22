# RWA Compliance Metadata API

## Overview
This document describes the compliance metadata management API for Real World Asset (RWA) tokens. The compliance metadata system provides comprehensive tracking of KYC/AML verification, regulatory compliance, and jurisdiction information for tokenized assets.

## Endpoints

### 1. Get Compliance Metadata

**Endpoint:** `GET /api/v1/compliance/{assetId}`

**Description:** Retrieves compliance metadata for a specific token.

**Authentication:** Required (ARC-0014)

**Parameters:**
- `assetId` (path, required): The asset ID of the token

**Response:** `ComplianceMetadataResponse`
```json
{
  "success": true,
  "metadata": {
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
  }
}
```

**Status Codes:**
- `200 OK`: Success
- `401 Unauthorized`: Missing or invalid authentication
- `404 Not Found`: Compliance metadata not found for this asset
- `500 Internal Server Error`: Server error

---

### 2. Create/Update Compliance Metadata

**Endpoint:** `POST /api/v1/compliance`

**Description:** Creates new compliance metadata or updates existing metadata for a token.

**Authentication:** Required (ARC-0014)

**Request Body:** `UpsertComplianceMetadataRequest`
```json
{
  "assetId": 12345,
  "kycProvider": "Sumsub",
  "kycVerificationDate": "2026-01-21T00:00:00Z",
  "verificationStatus": "Verified",
  "jurisdiction": "US,EU",
  "regulatoryFramework": "SEC Reg D",
  "complianceStatus": "Compliant",
  "lastComplianceReview": "2026-01-15T00:00:00Z",
  "nextComplianceReview": "2026-07-15T00:00:00Z",
  "assetType": "Security Token",
  "transferRestrictions": "Accredited investors only, lock-up period 12 months",
  "maxHolders": 500,
  "requiresAccreditedInvestors": true,
  "network": "voimain-v1.0",
  "notes": "Token represents equity in XYZ Corp"
}
```

**Field Descriptions:**
- `assetId` (required): Token asset ID
- `kycProvider`: Name of KYC/AML provider (max 200 chars)
- `kycVerificationDate`: Date when KYC verification completed
- `verificationStatus`: Pending, InProgress, Verified, Failed, or Expired
- `jurisdiction`: Country codes where token is compliant (max 500 chars)
- `regulatoryFramework`: Applicable regulations (max 500 chars)
- `complianceStatus`: UnderReview, Compliant, NonCompliant, Suspended, or Exempt
- `lastComplianceReview`: Date of last compliance review
- `nextComplianceReview`: Date when next review is due
- `assetType`: Type of tokenized asset (max 200 chars)
- `transferRestrictions`: Any transfer restrictions (max 1000 chars)
- `maxHolders`: Maximum number of token holders (1 to int.MaxValue)
- `requiresAccreditedInvestors`: Whether token requires accredited investors
- `network`: Blockchain network (voimain-v1.0, aramidmain-v1.0, etc.) (max 50 chars)
- `notes`: Additional compliance notes (max 2000 chars)

**Response:** `ComplianceMetadataResponse`

**Status Codes:**
- `200 OK`: Success
- `400 Bad Request`: Invalid request or network validation failure
- `401 Unauthorized`: Missing or invalid authentication
- `500 Internal Server Error`: Server error

---

### 3. Delete Compliance Metadata

**Endpoint:** `DELETE /api/v1/compliance/{assetId}`

**Description:** Deletes compliance metadata for a specific token.

**Authentication:** Required (ARC-0014)

**Parameters:**
- `assetId` (path, required): The asset ID of the token

**Response:** `ComplianceMetadataResponse`
```json
{
  "success": true
}
```

**Status Codes:**
- `200 OK`: Successfully deleted
- `401 Unauthorized`: Missing or invalid authentication
- `404 Not Found`: Compliance metadata not found
- `500 Internal Server Error`: Server error

---

### 4. List Compliance Metadata

**Endpoint:** `GET /api/v1/compliance`

**Description:** Lists compliance metadata with optional filtering and pagination.

**Authentication:** Required (ARC-0014)

**Query Parameters:**
- `complianceStatus` (optional): Filter by compliance status (UnderReview, Compliant, NonCompliant, Suspended, Exempt)
- `verificationStatus` (optional): Filter by verification status (Pending, InProgress, Verified, Failed, Expired)
- `network` (optional): Filter by network (max 50 chars)
- `page` (optional, default: 1): Page number (min: 1)
- `pageSize` (optional, default: 20, max: 100): Results per page

**Example:**
```
GET /api/v1/compliance?complianceStatus=Compliant&network=voimain-v1.0&page=1&pageSize=50
```

**Response:** `ComplianceMetadataListResponse`
```json
{
  "success": true,
  "metadata": [
    {
      "id": "guid1",
      "assetId": 12345,
      "complianceStatus": "Compliant",
      "network": "voimain-v1.0",
      ...
    },
    {
      "id": "guid2",
      "assetId": 67890,
      "complianceStatus": "Compliant",
      "network": "voimain-v1.0",
      ...
    }
  ],
  "totalCount": 150,
  "page": 1,
  "pageSize": 50,
  "totalPages": 3
}
```

**Status Codes:**
- `200 OK`: Success
- `400 Bad Request`: Invalid request parameters
- `401 Unauthorized`: Missing or invalid authentication
- `500 Internal Server Error`: Server error

---

## Network-Specific Validation Rules

### VOI Network (`voimain-v1.0`)

1. **Accredited Investor Tokens**: Tokens requiring accredited investors MUST have `verificationStatus` set to `Verified`
   - Error: "VOI network requires KYC verification (VerificationStatus=Verified) for tokens requiring accredited investors"

2. **Jurisdiction Requirement**: The `jurisdiction` field MUST be specified
   - Error: "VOI network requires jurisdiction to be specified for compliance"

### Aramid Network (`aramidmain-v1.0`)

1. **Compliant Status**: When `complianceStatus` is `Compliant`, the `regulatoryFramework` MUST be specified
   - Error: "Aramid network requires RegulatoryFramework to be specified when ComplianceStatus is Compliant"

2. **Security Tokens**: When `assetType` contains "security", the `maxHolders` field MUST be specified
   - Error: "Aramid network requires MaxHolders to be specified for security tokens"

---

## Enhanced Whitelist Entries

The whitelist entry model has been enhanced with compliance-related fields:

### New Fields in WhitelistEntry
- `reason`: Reason for whitelisting (e.g., "KYC verified", "Accredited investor")
- `expirationDate`: Optional expiration date for the whitelist entry
- `kycVerified`: Boolean indicating KYC verification status
- `kycVerificationDate`: Date when KYC was verified
- `kycProvider`: Name of the KYC provider

### Updated Endpoints

All whitelist endpoints now accept and return these additional fields:

**POST /api/v1/whitelist**
```json
{
  "assetId": 12345,
  "address": "ALGORAND_ADDRESS",
  "status": "Active",
  "reason": "KYC verified via Sumsub",
  "expirationDate": "2027-01-21T00:00:00Z",
  "kycVerified": true,
  "kycVerificationDate": "2026-01-15T00:00:00Z",
  "kycProvider": "Sumsub"
}
```

**POST /api/v1/whitelist/bulk**
```json
{
  "assetId": 12345,
  "addresses": ["ADDR1", "ADDR2", "ADDR3"],
  "status": "Active",
  "reason": "Batch KYC verification",
  "kycVerified": true,
  "kycVerificationDate": "2026-01-15T00:00:00Z",
  "kycProvider": "Onfido"
}
```

---

## Data Models

### VerificationStatus Enum
- `Pending`: Verification is pending
- `InProgress`: Verification is in progress
- `Verified`: Verification completed successfully
- `Failed`: Verification failed
- `Expired`: Verification expired and needs renewal

### ComplianceStatus Enum
- `UnderReview`: Compliance review is under review
- `Compliant`: Token is compliant with regulations
- `NonCompliant`: Token is non-compliant
- `Suspended`: Compliance status is suspended
- `Exempt`: Token is exempt from certain regulations

---

## Examples

### Example 1: Creating Compliance Metadata for VOI Network

**Request:**
```bash
curl -X POST https://api.example.com/api/v1/compliance \
  -H "Authorization: SigTx <signed-transaction>" \
  -H "Content-Type: application/json" \
  -d '{
    "assetId": 12345,
    "kycProvider": "Sumsub",
    "verificationStatus": "Verified",
    "jurisdiction": "US",
    "network": "voimain-v1.0",
    "requiresAccreditedInvestors": true,
    "complianceStatus": "Compliant"
  }'
```

**Response:**
```json
{
  "success": true,
  "metadata": {
    "id": "abc-123",
    "assetId": 12345,
    "kycProvider": "Sumsub",
    "verificationStatus": "Verified",
    "jurisdiction": "US",
    "network": "voimain-v1.0",
    "requiresAccreditedInvestors": true,
    "complianceStatus": "Compliant",
    "createdBy": "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA",
    "createdAt": "2026-01-21T21:00:00Z"
  }
}
```

### Example 2: Creating Compliance Metadata for Aramid Network

**Request:**
```bash
curl -X POST https://api.example.com/api/v1/compliance \
  -H "Authorization: SigTx <signed-transaction>" \
  -H "Content-Type: application/json" \
  -d '{
    "assetId": 67890,
    "network": "aramidmain-v1.0",
    "assetType": "Security Token",
    "maxHolders": 500,
    "complianceStatus": "Compliant",
    "regulatoryFramework": "MiFID II"
  }'
```

### Example 3: Network Validation Failure

**Request (VOI network without jurisdiction):**
```json
{
  "assetId": 12345,
  "network": "voimain-v1.0",
  "complianceStatus": "Compliant"
}
```

**Response (400 Bad Request):**
```json
{
  "success": false,
  "errorMessage": "VOI network requires jurisdiction to be specified for compliance"
}
```

---

## Transfer Validation API (NEW)

### Validate Transfer Endpoint

**Endpoint:** `POST /api/v1/whitelist/validate-transfer`

**Description:** Validates whether a token transfer between two addresses is permitted based on whitelist compliance rules. This endpoint supports MICA-aligned compliance flows for RWA tokens.

**Authentication:** Required (ARC-0014)

**Use Cases:**
- Pre-transfer compliance checks before executing blockchain transactions
- Real-time validation for trading platforms and exchanges
- Compliance verification for custodial services
- Regulatory reporting and audit trail generation

**Request Body:** `ValidateTransferRequest`
```json
{
  "assetId": 12345,
  "fromAddress": "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA",
  "toAddress": "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ",
  "amount": 1000
}
```

**Field Descriptions:**
- `assetId` (required): Token asset ID
- `fromAddress` (required): Sender's Algorand address (58 characters)
- `toAddress` (required): Receiver's Algorand address (58 characters)
- `amount` (optional): Transfer amount (for future use in amount-based restrictions)

**Response:** `ValidateTransferResponse`

**Success - Transfer Allowed:**
```json
{
  "success": true,
  "isAllowed": true,
  "denialReason": null,
  "senderStatus": {
    "address": "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA",
    "isWhitelisted": true,
    "isActive": true,
    "isExpired": false,
    "expirationDate": "2027-12-31T23:59:59Z",
    "status": "Active"
  },
  "receiverStatus": {
    "address": "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ",
    "isWhitelisted": true,
    "isActive": true,
    "isExpired": false,
    "expirationDate": null,
    "status": "Active"
  }
}
```

**Success - Transfer Denied:**
```json
{
  "success": true,
  "isAllowed": false,
  "denialReason": "Receiver address AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ is not whitelisted for asset 12345",
  "senderStatus": {
    "address": "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA",
    "isWhitelisted": true,
    "isActive": true,
    "isExpired": false,
    "expirationDate": null,
    "status": "Active"
  },
  "receiverStatus": {
    "address": "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ",
    "isWhitelisted": false,
    "isActive": false,
    "isExpired": false,
    "expirationDate": null,
    "status": null
  }
}
```

**Validation Rules:**
1. Both sender and receiver must be whitelisted for the specified asset
2. Both whitelist entries must have `status = "Active"`
3. Neither entry can be expired (if `expirationDate` is set)
4. Both addresses must be valid Algorand addresses (58 characters)

**Possible Denial Reasons:**
- Sender/Receiver not whitelisted
- Sender/Receiver status is Inactive or Revoked
- Sender/Receiver whitelist entry has expired
- Invalid address format

**Status Codes:**
- `200 OK`: Validation completed successfully (check `isAllowed` field)
- `400 Bad Request`: Invalid request parameters
- `401 Unauthorized`: Missing or invalid authentication
- `500 Internal Server Error`: Server error

**Example Usage:**

```bash
# Valid transfer
curl -X POST https://api.example.com/api/v1/whitelist/validate-transfer \
  -H "Authorization: SigTx <signed-transaction>" \
  -H "Content-Type: application/json" \
  -d '{
    "assetId": 12345,
    "fromAddress": "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA",
    "toAddress": "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ"
  }'
```

---

## Audit Log API (MICA/RWA Compliance)

### Overview

The audit log system provides comprehensive, immutable tracking of all compliance operations for regulatory reporting and incident investigations. All audit logs are retained for a minimum of 7 years to comply with MICA and other regulatory requirements.

### 5. Get Audit Log

**Endpoint:** `GET /api/v1/compliance/audit-log`

**Description:** Retrieves audit log entries with optional filtering and pagination. All compliance operations (create, update, delete, read, list) are automatically logged.

**Authentication:** Required (ARC-0014)

**Authorization:** Recommended for compliance and admin roles only

**Query Parameters:**
- `assetId` (optional): Filter by token asset ID
- `network` (optional): Filter by network (e.g., "voimain", "testnet")
- `actionType` (optional): Filter by action type (Create, Update, Delete, Read, List)
- `performedBy` (optional): Filter by user Algorand address
- `success` (optional): Filter by operation result (true/false)
- `fromDate` (optional): Start date filter (ISO 8601 format)
- `toDate` (optional): End date filter (ISO 8601 format)
- `page` (optional, default: 1): Page number
- `pageSize` (optional, default: 50, max: 100): Results per page

**Example:**
```
GET /api/v1/compliance/audit-log?assetId=12345&actionType=Update&fromDate=2026-01-01T00:00:00Z&page=1&pageSize=50
```

**Response:** `ComplianceAuditLogResponse`
```json
{
  "success": true,
  "entries": [
    {
      "id": "audit-guid-1",
      "assetId": 12345,
      "network": "voimain",
      "actionType": "Update",
      "performedBy": "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA",
      "performedAt": "2026-01-22T10:30:00Z",
      "success": true,
      "errorMessage": null,
      "oldComplianceStatus": "UnderReview",
      "newComplianceStatus": "Compliant",
      "oldVerificationStatus": "Pending",
      "newVerificationStatus": "Verified",
      "notes": "Updated compliance metadata",
      "itemCount": null,
      "filterCriteria": null
    }
  ],
  "totalCount": 150,
  "page": 1,
  "pageSize": 50,
  "totalPages": 3,
  "retentionPolicy": {
    "minimumRetentionYears": 7,
    "regulatoryFramework": "MICA",
    "immutableEntries": true,
    "description": "Audit logs are retained for a minimum of 7 years to comply with MICA and other regulatory requirements. All entries are immutable and cannot be modified or deleted."
  }
}
```

**Audit Entry Fields:**
- `id`: Unique identifier for the audit entry
- `assetId`: Token asset ID (null for list operations)
- `network`: Blockchain network
- `actionType`: Type of operation (Create, Update, Delete, Read, List)
- `performedBy`: Algorand address of the user who performed the action
- `performedAt`: Timestamp when the action was performed
- `success`: Whether the operation completed successfully
- `errorMessage`: Error message if the operation failed
- `oldComplianceStatus`: Previous compliance status (for updates)
- `newComplianceStatus`: New compliance status (for creates/updates)
- `oldVerificationStatus`: Previous verification status (for updates)
- `newVerificationStatus`: New verification status (for creates/updates)
- `notes`: Additional context about the operation
- `itemCount`: Number of items returned (for list operations)
- `filterCriteria`: Filter criteria applied (for list operations)

**Status Codes:**
- `200 OK`: Success
- `400 Bad Request`: Invalid request parameters
- `401 Unauthorized`: Missing or invalid authentication
- `500 Internal Server Error`: Server error

---

### 6. Export Audit Log (CSV)

**Endpoint:** `GET /api/v1/compliance/audit-log/export/csv`

**Description:** Exports audit log entries as a CSV file for regulatory compliance reporting. Maximum 10,000 records per export.

**Authentication:** Required (ARC-0014)

**Authorization:** Recommended for compliance and admin roles only

**Query Parameters:** Same as Get Audit Log endpoint (except page/pageSize)

**Example:**
```bash
curl -X GET "https://api.example.com/api/v1/compliance/audit-log/export/csv?fromDate=2026-01-01T00:00:00Z&toDate=2026-01-31T23:59:59Z" \
  -H "Authorization: SigTx <signed-transaction>" \
  -o compliance-audit-log.csv
```

**Response:** CSV file with headers
```csv
Id,AssetId,Network,ActionType,PerformedBy,PerformedAt,Success,ErrorMessage,OldComplianceStatus,NewComplianceStatus,OldVerificationStatus,NewVerificationStatus,ItemCount,FilterCriteria,Notes
"audit-guid-1",12345,"voimain","Update","VCMJKWOY...","2026-01-22T10:30:00Z",True,"","UnderReview","Compliant","Pending","Verified",,"","Updated compliance metadata"
```

**Status Codes:**
- `200 OK`: Success (returns CSV file)
- `400 Bad Request`: Invalid request parameters
- `401 Unauthorized`: Missing or invalid authentication
- `500 Internal Server Error`: Server error

---

### 7. Export Audit Log (JSON)

**Endpoint:** `GET /api/v1/compliance/audit-log/export/json`

**Description:** Exports audit log entries as a JSON file for regulatory compliance reporting. Maximum 10,000 records per export.

**Authentication:** Required (ARC-0014)

**Authorization:** Recommended for compliance and admin roles only

**Query Parameters:** Same as Get Audit Log endpoint (except page/pageSize)

**Example:**
```bash
curl -X GET "https://api.example.com/api/v1/compliance/audit-log/export/json?assetId=12345" \
  -H "Authorization: SigTx <signed-transaction>" \
  -o compliance-audit-log.json
```

**Response:** JSON file with complete audit log response structure (same as Get Audit Log endpoint)

**Status Codes:**
- `200 OK`: Success (returns JSON file)
- `400 Bad Request`: Invalid request parameters
- `401 Unauthorized`: Missing or invalid authentication
- `500 Internal Server Error`: Server error

---

### 8. Get Retention Policy

**Endpoint:** `GET /api/v1/compliance/audit-log/retention-policy`

**Description:** Returns metadata about the audit log retention policy.

**Authentication:** Required (ARC-0014)

**Response:** `AuditRetentionPolicy`
```json
{
  "minimumRetentionYears": 7,
  "regulatoryFramework": "MICA",
  "immutableEntries": true,
  "description": "Audit logs are retained for a minimum of 7 years to comply with MICA and other regulatory requirements. All entries are immutable and cannot be modified or deleted."
}
```

**Status Codes:**
- `200 OK`: Success
- `401 Unauthorized`: Missing or invalid authentication

---

## Audit Log Features

### Automatic Logging
All compliance operations are automatically logged:
- **Create**: New compliance metadata creation
- **Update**: Compliance metadata updates (tracks old and new values)
- **Delete**: Compliance metadata deletion
- **Read**: Single metadata retrieval
- **List**: Metadata listing with filters

### Immutability
- All audit log entries are immutable and cannot be modified or deleted
- Entries include unique IDs and timestamps for verification
- Perfect for regulatory compliance and forensic analysis

### Retention Policy
- Minimum retention period: **7 years**
- Regulatory framework: **MICA** (Markets in Crypto-Assets Regulation)
- Complies with RWA token regulatory requirements
- Suitable for enterprise audit trails

### Use Cases
1. **Regulatory Compliance Reporting**: Export audit logs for regulatory filings and inspections
2. **Incident Investigations**: Trace all compliance-related activities during security or compliance incidents
3. **Access Audits**: Monitor who accessed compliance data and when
4. **Change Tracking**: Track all changes to compliance metadata over time
5. **Compliance Dashboards**: Build real-time compliance monitoring dashboards

---

## Audit Log Examples

### Example 1: View Recent Compliance Changes

**Request:**
```bash
curl -X GET "https://api.example.com/api/v1/compliance/audit-log?actionType=Update&page=1&pageSize=10" \
  -H "Authorization: SigTx <signed-transaction>"
```

**Response:**
```json
{
  "success": true,
  "entries": [
    {
      "id": "audit-123",
      "assetId": 12345,
      "actionType": "Update",
      "performedBy": "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA",
      "performedAt": "2026-01-22T14:30:00Z",
      "success": true,
      "oldComplianceStatus": "UnderReview",
      "newComplianceStatus": "Compliant"
    }
  ],
  "totalCount": 1,
  "page": 1,
  "pageSize": 10,
  "totalPages": 1
}
```

### Example 2: Export Monthly Report

**Request:**
```bash
curl -X GET "https://api.example.com/api/v1/compliance/audit-log/export/csv?fromDate=2026-01-01T00:00:00Z&toDate=2026-01-31T23:59:59Z" \
  -H "Authorization: SigTx <signed-transaction>" \
  -o january-2026-compliance-audit.csv
```

**Result:** Downloads CSV file with all audit entries for January 2026

### Example 3: Investigate Failed Operations

**Request:**
```bash
curl -X GET "https://api.example.com/api/v1/compliance/audit-log?success=false&fromDate=2026-01-20T00:00:00Z" \
  -H "Authorization: SigTx <signed-transaction>"
```

**Response:**
```json
{
  "success": true,
  "entries": [
    {
      "id": "audit-456",
      "assetId": 12345,
      "actionType": "Create",
      "performedBy": "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA",
      "performedAt": "2026-01-21T10:15:00Z",
      "success": false,
      "errorMessage": "VOI network requires jurisdiction to be specified for compliance"
    }
  ]
}
```

### Example 4: Track Specific Token Changes

**Request:**
```bash
curl -X GET "https://api.example.com/api/v1/compliance/audit-log?assetId=12345&page=1&pageSize=50" \
  -H "Authorization: SigTx <signed-transaction>"
```

**Response:** Returns complete audit history for token 12345

---

## Best Practices

1. **Always specify network**: Include the `network` field to enable network-specific validation
2. **Keep compliance current**: Set `nextComplianceReview` dates and update metadata regularly
3. **Document jurisdictions**: Use standard country codes (ISO 3166-1 alpha-2) for `jurisdiction`
4. **Track KYC status**: Use the enhanced whitelist fields to maintain KYC verification status
5. **Set expiration dates**: Use `expirationDate` in whitelist entries for time-limited compliance
6. **Maintain audit trail**: All changes are tracked with `createdBy`, `updatedBy`, and timestamps
7. **Validate before transfers**: Use the transfer validation endpoint to check compliance before executing blockchain transactions
8. **Regular audit exports**: Export audit logs regularly for backup and compliance reporting (monthly/quarterly)
9. **Monitor failed operations**: Review failed operations in audit logs to identify compliance issues early
10. **Use date range filters**: When querying audit logs, use date ranges to optimize performance
11. **Secure audit access**: Restrict audit log access to compliance officers and administrators only
12. **Document retention**: Store exported audit logs according to your organization's retention policy (minimum 7 years)

---

## Migration Guide

### From Basic Whitelist to Enhanced Compliance

If you're already using the whitelist API, you can now enhance your entries with compliance data:

1. **Update existing whitelist entries** by calling POST /api/v1/whitelist with the new compliance fields
2. **Create compliance metadata** for each token using POST /api/v1/compliance
3. **Query compliance data** to generate reports or enforce transfer restrictions

### Storage

The current implementation uses thread-safe in-memory storage (ConcurrentDictionary). For production deployments with persistence requirements, the implementation can be easily swapped to a database backend without API changes.

---

## Support

For additional information or support, please refer to:
- Main API Documentation: `/swagger`
- Repository: https://github.com/scholtz/BiatecTokensApi
- Algorand Documentation: https://developer.algorand.org

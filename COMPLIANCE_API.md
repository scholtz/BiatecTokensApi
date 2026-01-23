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

## Compliance Attestation API (MICA/RWA Audit Trail)

### Overview

The Compliance Attestation API provides wallet-level compliance verification records that create an immutable audit trail for regulatory compliance. Attestations link cryptographic proof (IPFS CID, SHA-256 hash, etc.) to specific wallet addresses and tokens, enabling enterprise-grade compliance for RWA tokens under MICA and other regulatory frameworks.

### 9. Create Compliance Attestation

**Endpoint:** `POST /api/v1/compliance/attestations`

**Description:** Creates a new compliance attestation record linking a wallet address to a token with cryptographic proof of compliance verification.

**Authentication:** Required (ARC-0014)

**Authorization:** All authenticated users can create attestations for any token/wallet combination

**Use Cases:**
- Record KYC/AML verification for wallet addresses
- Document accreditation status for investors
- Track regulatory compliance certifications
- Maintain audit trail for MICA compliance

**Request Body:** `CreateComplianceAttestationRequest`
```json
{
  "walletAddress": "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA",
  "assetId": 12345,
  "issuerAddress": "ISSUER1AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ",
  "proofHash": "QmYwAPJzv5CZsnA625s3Xf2nemtYgPpHdWEz79ojWnPbdG",
  "proofType": "IPFS",
  "attestationType": "KYC",
  "network": "voimain-v1.0",
  "jurisdiction": "US,EU",
  "regulatoryFramework": "MICA",
  "expiresAt": "2027-01-23T00:00:00Z",
  "notes": "KYC verification completed via Sumsub"
}
```

**Field Descriptions:**
- `walletAddress` (required): The wallet address being attested (max 100 chars)
- `assetId` (required): Token asset ID this attestation applies to
- `issuerAddress` (required): Address of the issuer creating the attestation (max 100 chars)
- `proofHash` (required): Cryptographic hash of compliance proof document (max 200 chars)
  - Can be IPFS CID, SHA-256 hash, ARC19 hash, or other cryptographic identifier
- `proofType` (optional): Type of proof (e.g., "IPFS", "SHA256", "ARC19") (max 50 chars)
- `attestationType` (optional): Type of attestation (e.g., "KYC", "AML", "Accreditation", "License") (max 100 chars)
- `network` (optional): Blockchain network (e.g., "voimain-v1.0", "aramidmain-v1.0") (max 50 chars)
- `jurisdiction` (optional): Jurisdiction(s) applicable to this attestation (max 500 chars)
- `regulatoryFramework` (optional): Regulatory framework (e.g., "MICA", "SEC Reg D") (max 500 chars)
- `expiresAt` (optional): Expiration date for time-limited attestations
- `notes` (optional): Additional metadata or notes (max 2000 chars)

**Response:** `ComplianceAttestationResponse`
```json
{
  "success": true,
  "attestation": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "walletAddress": "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA",
    "assetId": 12345,
    "issuerAddress": "ISSUER1AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ",
    "proofHash": "QmYwAPJzv5CZsnA625s3Xf2nemtYgPpHdWEz79ojWnPbdG",
    "proofType": "IPFS",
    "verificationStatus": "Pending",
    "attestationType": "KYC",
    "network": "voimain-v1.0",
    "jurisdiction": "US,EU",
    "regulatoryFramework": "MICA",
    "issuedAt": "2026-01-23T16:48:00Z",
    "expiresAt": "2027-01-23T00:00:00Z",
    "verifiedAt": null,
    "verifierAddress": null,
    "notes": "KYC verification completed via Sumsub",
    "createdAt": "2026-01-23T16:48:00Z",
    "updatedAt": null,
    "createdBy": "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA",
    "updatedBy": null
  }
}
```

**Attestation Verification Status:**
- `Pending`: Attestation is pending verification
- `Verified`: Attestation has been verified and is valid
- `Failed`: Attestation verification failed
- `Expired`: Attestation has expired and needs renewal
- `Revoked`: Attestation has been revoked

**Status Codes:**
- `200 OK`: Attestation created successfully
- `400 Bad Request`: Invalid request (missing required fields)
- `401 Unauthorized`: Missing or invalid authentication
- `500 Internal Server Error`: Server error

**Metering:** This operation emits a metering event for billing analytics (category: Compliance, operation: Add)

---

### 10. List Compliance Attestations

**Endpoint:** `GET /api/v1/compliance/attestations`

**Description:** Lists compliance attestations with optional filtering and pagination. Returns attestations matching the specified criteria.

**Authentication:** Required (ARC-0014)

**Authorization:** All authenticated users can list attestations (read-only)

**Query Parameters:**
- `walletAddress` (optional): Filter by wallet address (max 100 chars)
- `assetId` (optional): Filter by token asset ID
- `issuerAddress` (optional): Filter by issuer address (max 100 chars)
- `verificationStatus` (optional): Filter by verification status (Pending, Verified, Failed, Expired, Revoked)
- `attestationType` (optional): Filter by attestation type (max 100 chars)
- `network` (optional): Filter by network (max 50 chars)
- `excludeExpired` (optional): If true, exclude expired attestations
- `fromDate` (optional): Start date filter (filter by IssuedAt)
- `toDate` (optional): End date filter (filter by IssuedAt)
- `page` (optional, default: 1): Page number (min: 1)
- `pageSize` (optional, default: 20, max: 100): Results per page

**Example:**
```
GET /api/v1/compliance/attestations?assetId=12345&verificationStatus=Verified&page=1&pageSize=50
```

**Response:** `ComplianceAttestationListResponse`
```json
{
  "success": true,
  "attestations": [
    {
      "id": "550e8400-e29b-41d4-a716-446655440000",
      "walletAddress": "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA",
      "assetId": 12345,
      "issuerAddress": "ISSUER1AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ",
      "proofHash": "QmYwAPJzv5CZsnA625s3Xf2nemtYgPpHdWEz79ojWnPbdG",
      "proofType": "IPFS",
      "verificationStatus": "Verified",
      "attestationType": "KYC",
      "network": "voimain-v1.0",
      "jurisdiction": "US,EU",
      "regulatoryFramework": "MICA",
      "issuedAt": "2026-01-23T16:48:00Z",
      "expiresAt": "2027-01-23T00:00:00Z",
      "verifiedAt": "2026-01-23T17:00:00Z",
      "verifierAddress": "VERIFIER1AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ",
      "notes": "KYC verification completed via Sumsub",
      "createdAt": "2026-01-23T16:48:00Z",
      "updatedAt": null,
      "createdBy": "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA",
      "updatedBy": null
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

**Note:** This is a read operation and does not emit metering events.

---

### 11. Get Compliance Attestation by ID

**Endpoint:** `GET /api/v1/compliance/attestations/{id}`

**Description:** Retrieves a specific compliance attestation by its unique identifier. Automatically marks expired attestations.

**Authentication:** Required (ARC-0014)

**Parameters:**
- `id` (path, required): The unique identifier of the attestation

**Response:** `ComplianceAttestationResponse`
```json
{
  "success": true,
  "attestation": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "walletAddress": "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA",
    "assetId": 12345,
    "issuerAddress": "ISSUER1AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ",
    "proofHash": "QmYwAPJzv5CZsnA625s3Xf2nemtYgPpHdWEz79ojWnPbdG",
    "proofType": "IPFS",
    "verificationStatus": "Verified",
    "attestationType": "KYC",
    "network": "voimain-v1.0",
    "jurisdiction": "US,EU",
    "regulatoryFramework": "MICA",
    "issuedAt": "2026-01-23T16:48:00Z",
    "expiresAt": "2027-01-23T00:00:00Z",
    "verifiedAt": "2026-01-23T17:00:00Z",
    "verifierAddress": "VERIFIER1AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ",
    "notes": "KYC verification completed via Sumsub",
    "createdAt": "2026-01-23T16:48:00Z",
    "updatedAt": null,
    "createdBy": "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA",
    "updatedBy": null
  }
}
```

**Status Codes:**
- `200 OK`: Success
- `401 Unauthorized`: Missing or invalid authentication
- `404 Not Found`: Attestation not found
- `500 Internal Server Error`: Server error

---

### 12. Export Attestations (JSON)

**Endpoint:** `GET /api/v1/compliance/attestations/export/json`

**Description:** Exports attestation records as a JSON file for regulatory compliance reporting. Maximum 10,000 records per export.

**Authentication:** Required (ARC-0014)

**Authorization:** Recommended for compliance and admin roles only

**Query Parameters:** Same as List Compliance Attestations endpoint (except page/pageSize)

**Example:**
```bash
curl -X GET "https://api.example.com/api/v1/compliance/attestations/export/json?assetId=12345&fromDate=2026-01-01T00:00:00Z&toDate=2026-01-31T23:59:59Z" \
  -H "Authorization: SigTx <signed-transaction>" \
  -o attestations-export.json
```

**Response:** JSON file with attestation list response structure

**Status Codes:**
- `200 OK`: Success (returns JSON file)
- `400 Bad Request`: Invalid request parameters
- `401 Unauthorized`: Missing or invalid authentication
- `500 Internal Server Error`: Server error

---

### 13. Export Attestations (CSV)

**Endpoint:** `GET /api/v1/compliance/attestations/export/csv`

**Description:** Exports attestation records as a CSV file for regulatory compliance reporting. Maximum 10,000 records per export.

**Authentication:** Required (ARC-0014)

**Authorization:** Recommended for compliance and admin roles only

**Query Parameters:** Same as List Compliance Attestations endpoint (except page/pageSize)

**Example:**
```bash
curl -X GET "https://api.example.com/api/v1/compliance/attestations/export/csv?assetId=12345" \
  -H "Authorization: SigTx <signed-transaction>" \
  -o attestations-export.csv
```

**Response:** CSV file with headers
```csv
Id,WalletAddress,AssetId,IssuerAddress,ProofHash,ProofType,VerificationStatus,AttestationType,Network,Jurisdiction,RegulatoryFramework,IssuedAt,ExpiresAt,VerifiedAt,VerifierAddress,Notes,CreatedAt,CreatedBy
"550e8400-e29b-41d4-a716-446655440000","VCMJKWOY...","12345","ISSUER1...","QmYwAPJzv...","IPFS","Verified","KYC","voimain-v1.0","US,EU","MICA","2026-01-23T16:48:00Z","2027-01-23T00:00:00Z","2026-01-23T17:00:00Z","VERIFIER1...","KYC verification completed via Sumsub","2026-01-23T16:48:00Z","VCMJKWOY..."
```

**Status Codes:**
- `200 OK`: Success (returns CSV file)
- `400 Bad Request`: Invalid request parameters
- `401 Unauthorized`: Missing or invalid authentication
- `500 Internal Server Error`: Server error

---

### 14. Generate Attestation Package (MICA Audit)

**Endpoint:** `POST /api/v1/compliance/attestation`

**Description:** Generates a comprehensive signed compliance attestation package for MICA regulatory audits. Combines token metadata, compliance status, whitelist information, and attestations into a single verifiable audit artifact.

**Authentication:** Required (ARC-0014)

**Authorization:** Token issuer/creator only (user must be authenticated)

**Request Body:** `GenerateAttestationPackageRequest`
```json
{
  "tokenId": 12345,
  "fromDate": "2026-01-01T00:00:00Z",
  "toDate": "2026-01-31T23:59:59Z",
  "format": "json"
}
```

**Field Descriptions:**
- `tokenId` (required): The token ID (asset ID) for which to generate the attestation package (must be > 0)
- `fromDate` (optional): Start date for the attestation package date range
- `toDate` (optional): End date for the attestation package date range (must not be earlier than fromDate)
- `format` (required): Output format, either "json" or "pdf" (default: "json")
  - Note: PDF format is validated but returns 501 Not Implemented (future enhancement)

**Response:** `AttestationPackageResponse`
```json
{
  "success": true,
  "package": {
    "packageId": "550e8400-e29b-41d4-a716-446655440000",
    "tokenId": 12345,
    "generatedAt": "2026-01-23T16:48:00Z",
    "issuerAddress": "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA",
    "network": "voimain-v1.0",
    "token": {
      "assetId": 12345,
      "name": "RWA Token",
      "unitName": "RWAT",
      "total": 1000000,
      "decimals": 6,
      "creator": "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA"
    },
    "complianceMetadata": {
      "assetId": 12345,
      "complianceStatus": "Compliant",
      "verificationStatus": "Verified",
      "regulatoryFramework": "MICA",
      "jurisdiction": "EU"
    },
    "whitelistPolicy": {
      "isEnabled": true,
      "totalWhitelisted": 50,
      "enforcementType": "Mandatory"
    },
    "complianceStatus": {
      "status": "Compliant",
      "verificationStatus": "Verified",
      "lastReviewDate": "2026-01-15T00:00:00Z",
      "nextReviewDate": "2026-04-15T00:00:00Z"
    },
    "attestations": [
      {
        "id": "att-001",
        "walletAddress": "WALLET...",
        "assetId": 12345,
        "issuerAddress": "ISSUER...",
        "proofHash": "QmYwAPJzv5CZsnA625s3Xf2nemtYgPpHdWEz79ojWnPbdG",
        "proofType": "IPFS",
        "verificationStatus": "Verified",
        "attestationType": "KYC",
        "network": "voimain-v1.0",
        "jurisdiction": "EU",
        "regulatoryFramework": "MICA",
        "issuedAt": "2026-01-20T00:00:00Z"
      }
    ],
    "dateRange": {
      "from": "2026-01-01T00:00:00Z",
      "to": "2026-01-31T23:59:59Z"
    },
    "contentHash": "a3f5c8d9e2b1f4a6c7e8d9f0a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0",
    "signature": {
      "algorithm": "SHA256",
      "publicKey": null,
      "signatureValue": null,
      "signedAt": "2026-01-23T16:48:00Z"
    }
  },
  "format": "json"
}
```

**Package Contents:**
- **Package ID**: Unique identifier for the package
- **Token metadata**: Basic token information (name, supply, decimals, etc.)
- **Compliance metadata**: Full compliance status and verification information
- **Whitelist policy**: Information about whitelist enforcement
- **Compliance status**: Current status and review dates
- **Attestations**: All attestations within the specified date range (up to 100)
- **Date range**: The filter range applied
- **Content hash**: Deterministic SHA-256 hash for verification
- **Signature metadata**: Structure for audit trail verification

**Status Codes:**
- `200 OK`: Package generated successfully
- `400 Bad Request`: Invalid request parameters
- `401 Unauthorized`: Missing or invalid authentication
- `404 Not Found`: Token metadata or compliance metadata not found
- `501 Not Implemented`: PDF format requested (future enhancement)
- `500 Internal Server Error`: Server error

**Metering:** This operation emits a metering event for billing analytics (category: Compliance, operation: Export)

---

## Attestation Features

### Immutability and Audit Trail
- All attestation records are immutable (append-only)
- Stored with unique IDs and timestamps for verification
- Created by user is tracked for accountability
- Perfect for regulatory compliance and forensic analysis

### Expiration Management
- Attestations can have optional expiration dates
- Expired attestations are automatically marked when retrieved
- Filter expired attestations using the `excludeExpired` parameter

### Cryptographic Proof
- Support for IPFS CIDs, SHA-256 hashes, and other cryptographic identifiers
- ProofType field allows specification of hash algorithm or storage system
- Content can be verified independently using the proof hash

### Regulatory Compliance
- Designed for MICA (Markets in Crypto-Assets) compliance
- Supports multiple jurisdictions and regulatory frameworks
- Attestation types include KYC, AML, Accreditation, License, etc.
- Export capabilities for regulatory reporting

### Use Cases
1. **KYC/AML Verification**: Document wallet-level KYC verification for RWA tokens
2. **Investor Accreditation**: Track accredited investor status with expiration
3. **Regulatory Audits**: Generate comprehensive audit packages for regulators
4. **License Tracking**: Maintain records of regulatory licenses and certifications
5. **Compliance Reporting**: Export attestations for quarterly/annual compliance reports

---

## Attestation Examples

### Example 1: Create KYC Attestation

**Request:**
```bash
curl -X POST https://api.example.com/api/v1/compliance/attestations \
  -H "Authorization: SigTx <signed-transaction>" \
  -H "Content-Type: application/json" \
  -d '{
    "walletAddress": "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA",
    "assetId": 12345,
    "issuerAddress": "ISSUER1AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ",
    "proofHash": "QmYwAPJzv5CZsnA625s3Xf2nemtYgPpHdWEz79ojWnPbdG",
    "proofType": "IPFS",
    "attestationType": "KYC",
    "network": "voimain-v1.0",
    "jurisdiction": "US",
    "regulatoryFramework": "MICA",
    "expiresAt": "2027-01-23T00:00:00Z",
    "notes": "KYC verification via Sumsub - Level 3 verification completed"
  }'
```

**Response:**
```json
{
  "success": true,
  "attestation": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "walletAddress": "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA",
    "assetId": 12345,
    "verificationStatus": "Pending",
    "attestationType": "KYC",
    "issuedAt": "2026-01-23T16:48:00Z",
    "expiresAt": "2027-01-23T00:00:00Z",
    "createdAt": "2026-01-23T16:48:00Z",
    "createdBy": "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA"
  }
}
```

### Example 2: List Verified Attestations for a Token

**Request:**
```bash
curl -X GET "https://api.example.com/api/v1/compliance/attestations?assetId=12345&verificationStatus=Verified&excludeExpired=true" \
  -H "Authorization: SigTx <signed-transaction>"
```

**Response:**
```json
{
  "success": true,
  "attestations": [
    {
      "id": "att-001",
      "walletAddress": "WALLET1...",
      "assetId": 12345,
      "verificationStatus": "Verified",
      "attestationType": "KYC",
      "issuedAt": "2026-01-20T00:00:00Z",
      "expiresAt": "2027-01-20T00:00:00Z"
    },
    {
      "id": "att-002",
      "walletAddress": "WALLET2...",
      "assetId": 12345,
      "verificationStatus": "Verified",
      "attestationType": "AML",
      "issuedAt": "2026-01-21T00:00:00Z",
      "expiresAt": "2027-01-21T00:00:00Z"
    }
  ],
  "totalCount": 2,
  "page": 1,
  "pageSize": 20,
  "totalPages": 1
}
```

### Example 3: Export Attestations for Audit

**Request:**
```bash
curl -X GET "https://api.example.com/api/v1/compliance/attestations/export/csv?assetId=12345&fromDate=2026-01-01T00:00:00Z&toDate=2026-01-31T23:59:59Z" \
  -H "Authorization: SigTx <signed-transaction>" \
  -o attestations-january-2026.csv
```

**Result:** Downloads CSV file with all attestations for token 12345 in January 2026

### Example 4: Generate MICA Attestation Package

**Request:**
```bash
curl -X POST https://api.example.com/api/v1/compliance/attestation \
  -H "Authorization: SigTx <signed-transaction>" \
  -H "Content-Type: application/json" \
  -d '{
    "tokenId": 12345,
    "fromDate": "2026-01-01T00:00:00Z",
    "toDate": "2026-01-31T23:59:59Z",
    "format": "json"
  }'
```

**Response:** Complete attestation package with token metadata, compliance status, and all attestations in the date range

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

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

## Best Practices

1. **Always specify network**: Include the `network` field to enable network-specific validation
2. **Keep compliance current**: Set `nextComplianceReview` dates and update metadata regularly
3. **Document jurisdictions**: Use standard country codes (ISO 3166-1 alpha-2) for `jurisdiction`
4. **Track KYC status**: Use the enhanced whitelist fields to maintain KYC verification status
5. **Set expiration dates**: Use `expirationDate` in whitelist entries for time-limited compliance
6. **Maintain audit trail**: All changes are tracked with `createdBy`, `updatedBy`, and timestamps

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

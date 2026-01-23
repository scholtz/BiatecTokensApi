# MICA Compliance Attestation Package Implementation

## Overview
This document describes the implementation of the MICA compliance attestation export endpoint for generating signed compliance attestation packages for regulatory audits.

## Endpoint Details

### POST /api/v1/compliance/attestation

**Purpose**: Generates verifiable audit artifacts for regulators and enterprise issuers.

**Authentication**: Requires ARC-0014 authentication

**Request Body**:
```json
{
  "tokenId": 12345,
  "fromDate": "2026-01-01T00:00:00Z",
  "toDate": "2026-01-31T23:59:59Z",
  "format": "json"
}
```

**Request Parameters**:
- `tokenId` (required): The token ID (asset ID) for which to generate the attestation package
  - Must be greater than zero
- `fromDate` (optional): Start date for the attestation package date range
- `toDate` (optional): End date for the attestation package date range
  - Must not be earlier than fromDate if both are provided
- `format` (required): Output format, either "json" or "pdf"
  - Default: "json"
  - PDF format is validated but returns 501 Not Implemented (future enhancement)

**Response Body**:
```json
{
  "success": true,
  "package": {
    "packageId": "550e8400-e29b-41d4-a716-446655440000",
    "tokenId": 12345,
    "generatedAt": "2026-01-23T12:00:00Z",
    "issuerAddress": "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA",
    "network": "voimain-v1.0",
    "token": {
      "assetId": 12345
    },
    "complianceMetadata": {
      "assetId": 12345,
      "complianceStatus": "Compliant",
      "verificationStatus": "Verified",
      "regulatoryFramework": "MICA",
      "jurisdiction": "EU"
    },
    "whitelistPolicy": {
      "isEnabled": false,
      "totalWhitelisted": 0,
      "enforcementType": "None"
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
    "contentHash": "r4X5c8nQZ7...",
    "signature": {
      "algorithm": "SHA256",
      "signedAt": "2026-01-23T12:00:00Z"
    }
  },
  "format": "json"
}
```

## Features

### Validation
- ✅ TokenId must be greater than zero
- ✅ FromDate must not be greater than ToDate
- ✅ Format must be either "json" or "pdf"
- ✅ All required fields validated by model annotations

### Attestation Package Contents
- ✅ **Package ID**: Unique identifier for the package
- ✅ **Token ID**: Asset ID of the token
- ✅ **Generation timestamp**: When the package was created
- ✅ **Issuer address**: Address of the user who requested the package
- ✅ **Network**: Blockchain network the token is deployed on
- ✅ **Token metadata**: Basic token information (placeholder, see limitations)
- ✅ **Compliance metadata**: Full compliance information including status, verification, framework, jurisdiction
- ✅ **Whitelist policy**: Information about whitelist enforcement (placeholder, see limitations)
- ✅ **Compliance status**: Current status and review dates
- ✅ **Attestations**: All attestations within the specified date range (up to 100)
- ✅ **Date range**: The filter range applied
- ✅ **Content hash**: Deterministic SHA-256 hash for verification
- ✅ **Signature metadata**: Structure for audit trail (placeholder, see limitations)

### Metering
- ✅ Emits metering event for billing analytics
- ✅ Tracks: category, operation type, asset ID, network, item count
- ✅ Metadata includes: format, type, attestation count, date range, content hash

## Testing

### Test Coverage
- ✅ 13 comprehensive integration tests
- ✅ All tests passing
- ✅ Test scenarios include:
  - Valid JSON request
  - Invalid format
  - Date range validation
  - No metadata scenario
  - Date range filtering
  - Multiple attestations
  - Deterministic hash generation
  - Controller validation
  - Service failures
  - PDF format handling
  - Exception handling
  - Response schema validation

### Running Tests
```bash
dotnet test --filter "FullyQualifiedName~AttestationPackageTests"
```

## Security

### Security Analysis
- ✅ CodeQL security scan: No vulnerabilities found
- ✅ Input validation on all parameters
- ✅ Authentication required (ARC-0014)
- ✅ Deterministic content hash for verification
- ✅ Signature metadata structure for audit trail

## Known Limitations

### 1. PDF Format (Future Enhancement)
- **Status**: Validated but not implemented
- **Behavior**: Returns 501 Not Implemented
- **Impact**: Users can only export JSON format currently
- **Recommendation**: Implement PDF generation using a library like QuestPDF or iTextSharp

### 2. Whitelist Policy Data (Placeholder)
- **Status**: Returns hardcoded placeholder values
- **Impact**: Whitelist information not accurate in attestation packages
- **Recommendation**: Integrate with WhitelistService to retrieve actual whitelist data
- **Required**: Add dependency on IWhitelistService in ComplianceService

### 3. Token Metadata (Placeholder)
- **Status**: Only includes AssetId, no blockchain data
- **Impact**: Missing creator, manager, reserve, freeze, clawback addresses
- **Recommendation**: Integrate with blockchain service (Algorand or EVM) to retrieve actual token parameters
- **Required**: Query Algorand API or EVM RPC for token details

### 4. Cryptographic Signature (Not Implemented)
- **Status**: Signature metadata structure only, no actual signature
- **Impact**: Packages are not cryptographically signed
- **Recommendation**: Implement actual signature using private key from secure key management system
- **Required**: 
  - Key management integration
  - Sign the ContentHash with issuer's private key
  - Include signature value and public key in response
  - Consider using Algorand's signing capabilities or standard ECDSA/EdDSA

### 5. Attestation Pagination
- **Status**: Limited to 100 attestations per package
- **Impact**: Tokens with more than 100 attestations will have incomplete packages
- **Recommendation**: Implement full pagination or configurable page size

## MICA Compliance Alignment

The attestation package aligns with MICA (Markets in Crypto-Assets) reporting requirements by providing:

1. **Complete Audit Trail**: All attestations within date range
2. **Verifiable Cryptographic Proof**: Deterministic content hash (signature structure ready)
3. **Regulatory Framework Information**: Jurisdiction and regulatory compliance metadata
4. **KYC/AML Status**: Verification status and compliance status
5. **Network Information**: Blockchain network deployment details
6. **Timestamped Records**: All operations are timestamped for audit trail

## Use Cases

1. **Regulatory Audit Submissions**: Export package for regulator review
2. **Enterprise Compliance Reporting**: Quarterly/annual compliance reviews
3. **Investor Disclosure**: Provide compliance status to potential investors
4. **Third-Party Audits**: Share verifiable compliance data with auditors
5. **Internal Compliance**: Track and verify compliance over time

## API Examples

### Example 1: Generate attestation package for a specific token
```bash
curl -X POST https://api.example.com/api/v1/compliance/attestation \
  -H "Authorization: SigTx <signed-transaction>" \
  -H "Content-Type: application/json" \
  -d '{
    "tokenId": 12345,
    "format": "json"
  }'
```

### Example 2: Generate package with date range filter
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

## Implementation Details

### Files Modified
1. `BiatecTokensApi/Models/Compliance/ComplianceAttestation.cs`
   - Added `GenerateAttestationPackageRequest`
   - Added `AttestationPackage` and related models
   - Added `AttestationPackageResponse`

2. `BiatecTokensApi/Services/Interface/IComplianceService.cs`
   - Added `GenerateAttestationPackageAsync` method

3. `BiatecTokensApi/Services/ComplianceService.cs`
   - Implemented `GenerateAttestationPackageAsync`
   - Implemented `GeneratePackageHash` helper method

4. `BiatecTokensApi/Controllers/ComplianceController.cs`
   - Added POST /compliance/attestation endpoint

5. `BiatecTokensTests/AttestationPackageTests.cs`
   - New test file with 13 comprehensive tests

## Future Enhancements

1. **PDF Export**: Implement PDF generation with formatted compliance report
2. **Whitelist Integration**: Retrieve actual whitelist data
3. **Blockchain Integration**: Query token metadata from Algorand/EVM networks
4. **Actual Signatures**: Implement cryptographic signing with key management
5. **Pagination**: Support larger attestation sets
6. **Caching**: Cache generated packages for repeated requests
7. **Localization**: Support multiple languages for regulatory reports
8. **Email Delivery**: Option to email packages to stakeholders
9. **Scheduled Generation**: Automated periodic package generation

## Subscription Integration

The endpoint supports subscription-based access control through metering:
- Operation type: `Export`
- Category: `Compliance`
- Enables tracking for subscription tier gating
- Allows billing based on export usage

## Maintenance Notes

- Content hash is deterministic based on package content
- Generated packages include timestamp, so repeated requests produce different hashes
- Metering events are emitted for all successful package generations
- Failed requests are logged but do not emit metering events
- The service is stateless and thread-safe

## Support

For questions or issues:
1. Check the test suite for usage examples
2. Review the OpenAPI/Swagger documentation at /swagger
3. Consult the inline code documentation
4. Refer to MICA compliance guidelines for regulatory requirements

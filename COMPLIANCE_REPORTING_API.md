# Compliance Reporting API Usage Examples

This document demonstrates how to use the new compliance reporting endpoints.

## Authentication

All endpoints require ARC-0014 authentication. You must include a signed Algorand transaction in the `Authorization` header:

```
Authorization: SigTx <base64-encoded-signed-transaction>
```

## Endpoints

### 1. Create a MICA Readiness Report

**Request:**
```http
POST /api/v1/compliance/reports
Content-Type: application/json

{
  "reportType": "MicaReadiness",
  "assetId": 12345,
  "network": "voimain-v1.0"
}
```

**Response:**
```json
{
  "success": true,
  "reportId": "550e8400-e29b-41d4-a716-446655440000",
  "status": "Completed",
  "createdAt": "2026-02-03T05:00:00Z"
}
```

### 2. Create an Audit Trail Report

**Request:**
```http
POST /api/v1/compliance/reports
Content-Type: application/json

{
  "reportType": "AuditTrail",
  "assetId": 12345,
  "network": "voimain-v1.0",
  "fromDate": "2026-01-01T00:00:00Z",
  "toDate": "2026-01-31T23:59:59Z"
}
```

**Response:**
```json
{
  "success": true,
  "reportId": "660e8400-e29b-41d4-a716-446655440001",
  "status": "Completed",
  "createdAt": "2026-02-03T05:00:00Z"
}
```

### 3. Create a Compliance Badge Report

**Request:**
```http
POST /api/v1/compliance/reports
Content-Type: application/json

{
  "reportType": "ComplianceBadge",
  "assetId": 12345,
  "network": "voimain-v1.0"
}
```

**Response:**
```json
{
  "success": true,
  "reportId": "770e8400-e29b-41d4-a716-446655440002",
  "status": "Completed",
  "createdAt": "2026-02-03T05:00:00Z"
}
```

### 4. List Reports

**Request:**
```http
GET /api/v1/compliance/reports?reportType=MicaReadiness&status=Completed&page=1&pageSize=50
```

**Response:**
```json
{
  "success": true,
  "reports": [
    {
      "reportId": "550e8400-e29b-41d4-a716-446655440000",
      "reportType": "MicaReadiness",
      "status": "Completed",
      "assetId": 12345,
      "network": "voimain-v1.0",
      "eventCount": 7,
      "createdAt": "2026-02-03T05:00:00Z",
      "completedAt": "2026-02-03T05:00:01Z",
      "warningCount": 2
    }
  ],
  "totalCount": 1,
  "page": 1,
  "pageSize": 50,
  "totalPages": 1
}
```

### 5. Get Report Details

**Request:**
```http
GET /api/v1/compliance/reports/550e8400-e29b-41d4-a716-446655440000
```

**Response:**
```json
{
  "success": true,
  "report": {
    "reportId": "550e8400-e29b-41d4-a716-446655440000",
    "reportType": "MicaReadiness",
    "issuerId": "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA",
    "assetId": 12345,
    "network": "voimain-v1.0",
    "fromDate": null,
    "toDate": null,
    "status": "Completed",
    "schemaVersion": "1.0",
    "createdAt": "2026-02-03T05:00:00Z",
    "completedAt": "2026-02-03T05:00:01Z",
    "errorMessage": null,
    "eventCount": 7,
    "checksum": "abc123def456...",
    "warnings": [
      "Article 20: Establish and document complaint handling process",
      "Article 31-35: Not applicable to this token type"
    ],
    "contentJson": "{...}" // Full report content
  }
}
```

### 6. Download Report as JSON

**Request:**
```http
GET /api/v1/compliance/reports/550e8400-e29b-41d4-a716-446655440000/download?format=json
```

**Response:**
- Content-Type: `application/json`
- File: `compliance-report-550e8400-e29b-41d4-a716-446655440000.json`

**Content Example:**
```json
{
  "metadata": {
    "reportId": "550e8400-e29b-41d4-a716-446655440000",
    "schemaVersion": "1.0",
    "generatedAt": "2026-02-03T05:00:01Z",
    "assetId": 12345,
    "network": "voimain-v1.0"
  },
  "complianceChecks": [
    {
      "article": "Article 17",
      "requirement": "Issuer must be properly authorized and registered",
      "status": "Pass",
      "evidence": "Issuer profile exists and is verified",
      "recommendation": null
    },
    {
      "article": "Article 18",
      "requirement": "Comprehensive disclosure of token information",
      "status": "Partial",
      "evidence": "Token metadata available in compliance records",
      "recommendation": "Ensure whitepaper and risk disclosure documents are attached"
    }
  ],
  "missingEvidence": [
    "Article 20: Establish and document complaint handling process"
  ],
  "readinessScore": 75,
  "readinessSummary": "Token shows moderate MICA compliance with some gaps to address"
}
```

### 7. Download Report as CSV

**Request:**
```http
GET /api/v1/compliance/reports/550e8400-e29b-41d4-a716-446655440000/download?format=csv
```

**Response:**
- Content-Type: `text/csv`
- File: `compliance-report-550e8400-e29b-41d4-a716-446655440000.csv`

**Content Example:**
```csv
# Compliance Report Export
# Report ID: 550e8400-e29b-41d4-a716-446655440000
# Report Type: MicaReadiness
# Generated: 2026-02-03 05:00:01 UTC
# Schema Version: 1.0
# Checksum: abc123def456...

Article,Requirement,Status,Evidence,Recommendation
"Article 17","Issuer must be properly authorized and registered","Pass","Issuer profile exists and is verified",""
"Article 18","Comprehensive disclosure of token information","Partial","Token metadata available in compliance records","Ensure whitepaper and risk disclosure documents are attached"
"Article 19","Clear documentation of token holder rights and obligations","Partial","Transfer restrictions documented","Add detailed rights documentation to compliance metadata"

# Readiness Score: 75%
# Summary: Token shows moderate MICA compliance with some gaps to address
```

## Filter Parameters

### List Reports Filters

- `reportType`: Filter by report type (MicaReadiness, AuditTrail, ComplianceBadge)
- `assetId`: Filter by token asset ID
- `network`: Filter by network (voimain-v1.0, aramidmain-v1.0, etc.)
- `status`: Filter by status (Pending, Processing, Completed, Failed)
- `fromDate`: Filter by creation date (from)
- `toDate`: Filter by creation date (to)
- `page`: Page number for pagination (default: 1)
- `pageSize`: Items per page (default: 50, max: 100)

### Create Report Filters

- `reportType`: Required - Type of report to generate
- `assetId`: Optional - Filter to specific token
- `network`: Optional - Filter to specific network
- `fromDate`: Optional - Start of reporting period
- `toDate`: Optional - End of reporting period

## Report Types

### MICA Readiness Report

Assesses token compliance against MICA Articles 17-35:
- Authorization requirements (Article 17)
- Transparency and disclosure (Article 18)
- Token holder rights (Article 19)
- Complaint handling (Article 20)
- Operational procedures (Articles 21-25)
- Risk management (Articles 26-30)
- Reserve assets (Articles 31-35)

Returns:
- List of compliance checks with pass/fail/partial status
- Evidence for each check
- Recommendations for addressing gaps
- Overall readiness score (0-100)
- Summary of readiness status

### Audit Trail Report

Chronological snapshot of all audit events:
- Token issuance events
- Compliance metadata changes
- Whitelist/blacklist operations
- Transfer validations
- Transaction confirmations

Returns:
- List of audit events ordered by timestamp
- Summary statistics (event counts by category)
- Date range information
- Network and asset coverage

### Compliance Badge Report

Evidence collection for compliance certification:
- Audit trail evidence
- Compliance metadata evidence
- KYC verification evidence
- Deployment confirmation
- Regulatory adherence proof

Returns:
- List of evidence items with verification status
- Badge eligibility status (Eligible/Incomplete)
- Missing requirements list

## Error Handling

### Report Not Found
```json
{
  "success": false,
  "errorMessage": "Report not found or access denied"
}
```

### Report Not Ready
```json
{
  "success": false,
  "errorMessage": "Report is not ready for download. Status: Processing"
}
```

### Unsupported Format
```json
{
  "success": false,
  "errorMessage": "Unsupported format: xml. Supported formats: json, csv"
}
```

## Access Control

- Reports are scoped to the authenticated issuer
- You can only create and view reports for tokens you have issued
- Attempting to access another issuer's reports returns 404
- Admin access is not implemented in the current version

## Best Practices

1. **Generate reports periodically** for ongoing compliance monitoring
2. **Archive downloaded reports** with checksums for tamper evidence
3. **Review warnings** in MICA readiness reports to address gaps
4. **Use date filters** for audit trails to manage large datasets
5. **Verify checksums** when downloading reports for integrity verification
6. **Use CSV format** for human review and spreadsheet analysis
7. **Use JSON format** for programmatic processing and integration

## Integration Example (cURL)

```bash
# Create a MICA readiness report
curl -X POST https://api.example.com/api/v1/compliance/reports \
  -H "Authorization: SigTx <your-signed-transaction>" \
  -H "Content-Type: application/json" \
  -d '{
    "reportType": "MicaReadiness",
    "assetId": 12345,
    "network": "voimain-v1.0"
  }'

# List reports
curl https://api.example.com/api/v1/compliance/reports \
  -H "Authorization: SigTx <your-signed-transaction>"

# Download report as CSV
curl https://api.example.com/api/v1/compliance/reports/<report-id>/download?format=csv \
  -H "Authorization: SigTx <your-signed-transaction>" \
  -o compliance-report.csv
```

# Token Registry and Compliance Scoring API

## Overview

The Token Registry API provides a centralized, queryable data source for token identity, issuer information, compliance state, and operational readiness across multiple blockchain networks and token standards. This document outlines the backend architecture, API endpoints, and usage patterns for the registry system.

## Architecture

### Data Models

#### TokenRegistryEntry
The canonical registry model that aggregates token data from internal and external sources:
- **Identity**: TokenIdentifier, Chain, Name, Symbol, Decimals, TotalSupply
- **Standards**: SupportedStandards, PrimaryStandard
- **Issuer**: IssuerIdentity (name, address, verification status)
- **Compliance**: ComplianceScoring (status, score, regulatory frameworks, jurisdictions)
- **Operational Readiness**: Contract verification, audits, metadata validity, security features
- **Metadata**: Description, Website, LogoUrl, Tags, ExternalRegistries

#### Compliance Taxonomy
Explicit compliance states:
- **Unknown**: Compliance status not yet evaluated
- **Pending**: Compliance review in progress
- **Compliant**: Token meets relevant regulations
- **NonCompliant**: Token does not meet regulations
- **Suspended**: Compliance status suspended pending review
- **Exempt**: Token exempt from certain regulations

### Repository Layer

**TokenRegistryRepository** (`Repositories/TokenRegistryRepository.cs`)
- In-memory implementation using `ConcurrentDictionary` for thread-safe operations
- Supports filtering by standard, compliance status, chain, issuer, and readiness
- Implements pagination with configurable page size (1-100 items)
- Idempotent upsert operations based on TokenIdentifier + Chain combination
- Full-text search across name, symbol, and token identifier

### Service Layer

**TokenRegistryService** (`Services/TokenRegistryService.cs`)
- Business logic for validation and normalization
- Validates required fields, chain format, token standards, and compliance scores
- Normalizes chain names to standard format (e.g., "algorand-mainnet")
- Normalizes standards to uppercase (e.g., "ARC3", "ERC20")
- Returns structured validation results with errors, warnings, and info messages

**RegistryIngestionService** (`Services/RegistryIngestionService.cs`)
- Ingests token data from internal token deployment records
- Normalizes data from heterogeneous sources
- Maps internal compliance metadata to registry format
- Logs anomalies for review without blocking ingestion
- Idempotent processing - reruns do not create duplicates

## API Endpoints

All endpoints require ARC-0014 authentication. Base path: `/api/v1/registry`

### 1. List Tokens

**GET** `/api/v1/registry/tokens`

Returns a paginated list of tokens with optional filtering.

**Query Parameters:**
- `standard` (string): Filter by token standard (ASA, ARC3, ARC200, ERC20, etc.)
- `complianceStatus` (enum): Filter by compliance state (Unknown, Pending, Compliant, NonCompliant, Suspended, Exempt)
- `chain` (string): Filter by blockchain network (algorand-mainnet, base-mainnet, etc.)
- `issuerAddress` (string): Filter by issuer address
- `isContractVerified` (boolean): Filter by contract verification status
- `isAudited` (boolean): Filter by audit status
- `hasValidMetadata` (boolean): Filter by metadata validity
- `search` (string): Search by name, symbol, or identifier
- `tags` (string): Filter by tags (comma-separated)
- `dataSource` (string): Filter by data source
- `page` (integer): Page number (1-based, default: 1)
- `pageSize` (integer): Items per page (1-100, default: 20)
- `sortBy` (string): Sort field (name, symbol, createdAt, updatedAt, complianceScore, deployedAt)
- `sortDirection` (string): Sort direction (asc, desc, default: desc)

**Example Request:**
```
GET /api/v1/registry/tokens?standard=ARC3&complianceStatus=Compliant&page=1&pageSize=20
```

**Example Response:**
```json
{
  "tokens": [
    {
      "id": "uuid-here",
      "tokenIdentifier": "123456",
      "chain": "algorand-mainnet",
      "name": "My ARC3 Token",
      "symbol": "MYT",
      "decimals": 6,
      "totalSupply": "1000000",
      "supportedStandards": ["ASA", "ARC3"],
      "primaryStandard": "ARC3",
      "issuer": {
        "name": "My Company",
        "address": "ABCD...",
        "isVerified": true,
        "verificationProvider": "KYC Provider"
      },
      "compliance": {
        "status": "Compliant",
        "score": 95,
        "regulatoryFrameworks": ["MICA"],
        "jurisdictions": ["EU"],
        "lastReviewDate": "2026-01-15T00:00:00Z"
      },
      "readiness": {
        "isContractVerified": true,
        "isAudited": true,
        "hasValidMetadata": true,
        "auditReports": [
          {
            "auditor": "Security Firm",
            "auditDate": "2025-12-01T00:00:00Z",
            "reportUrl": "https://...",
            "result": "Pass"
          }
        ]
      },
      "createdAt": "2026-01-01T00:00:00Z",
      "updatedAt": "2026-01-15T00:00:00Z"
    }
  ],
  "totalCount": 150,
  "page": 1,
  "pageSize": 20,
  "totalPages": 8,
  "hasNextPage": true,
  "hasPreviousPage": false
}
```

### 2. Get Token Details

**GET** `/api/v1/registry/tokens/{identifier}`

Returns complete details for a single token.

**Path Parameters:**
- `identifier` (string): Registry ID, token identifier (asset ID/contract address), or symbol

**Query Parameters:**
- `chain` (string, optional): Chain filter to disambiguate symbols

**Example Request:**
```
GET /api/v1/registry/tokens/123456
GET /api/v1/registry/tokens/USDC?chain=algorand-mainnet
```

**Example Response:**
```json
{
  "found": true,
  "token": {
    "id": "uuid-here",
    "tokenIdentifier": "123456",
    // ... full token details
  }
}
```

**Error Response (404):**
```json
{
  "found": false,
  "errorMessage": "Token not found: 123456"
}
```

### 3. Create or Update Token

**POST** `/api/v1/registry/tokens`

Creates a new token registry entry or updates an existing one (idempotent upsert).

**Request Body:**
```json
{
  "tokenIdentifier": "123456",
  "chain": "algorand-mainnet",
  "name": "My Token",
  "symbol": "MTK",
  "decimals": 6,
  "totalSupply": "1000000",
  "supportedStandards": ["ASA", "ARC3"],
  "primaryStandard": "ARC3",
  "issuer": {
    "name": "My Company",
    "address": "ABCD...",
    "isVerified": true
  },
  "compliance": {
    "status": "Compliant",
    "regulatoryFrameworks": ["MICA"],
    "jurisdictions": ["EU"]
  },
  "readiness": {
    "isContractVerified": true,
    "isAudited": true,
    "hasValidMetadata": true
  },
  "dataSource": "internal"
}
```

**Example Response:**
```json
{
  "success": true,
  "registryId": "uuid-here",
  "created": true,
  "token": {
    // ... complete token entry
  }
}
```

### 4. Search Tokens

**GET** `/api/v1/registry/search`

Quick search for tokens by name, symbol, or identifier.

**Query Parameters:**
- `q` (string, required): Search query
- `limit` (integer): Maximum results (1-50, default: 10)

**Example Request:**
```
GET /api/v1/registry/search?q=USDC&limit=10
```

**Example Response:**
```json
[
  {
    "id": "uuid-1",
    "tokenIdentifier": "31566704",
    "chain": "algorand-mainnet",
    "name": "USDC",
    "symbol": "USDC",
    // ... other fields
  }
]
```

### 5. Trigger Ingestion

**POST** `/api/v1/registry/ingest`

Manually triggers ingestion of token data from internal or external sources.

**Request Body:**
```json
{
  "source": "internal",
  "chain": "algorand-mainnet",
  "force": false,
  "limit": 100
}
```

**Parameters:**
- `source` (string): Data source to ingest from ("internal", "all")
- `chain` (string, optional): Filter to specific chain
- `force` (boolean): Force re-ingestion of existing entries
- `limit` (integer, optional): Maximum entries to process (for testing)

**Example Response:**
```json
{
  "success": true,
  "processedCount": 50,
  "createdCount": 45,
  "updatedCount": 5,
  "skippedCount": 0,
  "errorCount": 0,
  "anomalies": [],
  "errors": [],
  "duration": "00:00:05.123",
  "startedAt": "2026-02-04T15:00:00Z",
  "completedAt": "2026-02-04T15:00:05Z"
}
```

## Data Ingestion

### Internal Token Ingestion

The system automatically ingests tokens from internal deployment records:

1. **Source**: `TokenIssuanceAuditLogEntry` records
2. **Filtering**: Only successful deployments (`Success = true`)
3. **Normalization**:
   - Chain names normalized to standard format
   - Standards determined from token type
   - Compliance data mapped from internal compliance metadata
4. **Idempotency**: Existing entries are not duplicated on re-run

### Adding External Sources

To add new external registry sources:

1. Implement data fetching in `RegistryIngestionService.IngestAsync()`
2. Add normalization logic in `NormalizeTokenDataAsync()`
3. Handle authentication/API keys via configuration
4. Log anomalies using `ValidateAndLogAnomaliesAsync()`

## Validation

### Required Fields
- TokenIdentifier (non-empty)
- Chain (non-empty)
- Name (non-empty)
- Symbol (non-empty)

### Field Validation
- Chain must match standard format (warnings for non-standard)
- Standards must be recognized (ASA, ARC3, ARC19, ARC69, ARC200, ARC1400, ERC20, ERC721, ERC1155)
- Compliance score must be 0-100
- URLs must be valid absolute URLs

### Normalization
- Chain names: lowercase with hyphens (e.g., "algorand-mainnet")
- Standards: uppercase (e.g., "ARC3", "ERC20")
- Tags: lowercase
- String fields: trimmed

## Error Handling

All endpoints return structured error responses:

```json
{
  "success": false,
  "errorCode": "VALIDATION_FAILED",
  "errorMessage": "Token identifier is required",
  "timestamp": "2026-02-04T15:00:00Z"
}
```

**Error Codes:**
- `InvalidRequest`: Invalid request parameters
- `ValidationFailed`: Validation errors
- `NotFound`: Resource not found
- `InternalError`: Server error

## Performance Considerations

### In-Memory Storage
- Current implementation uses `ConcurrentDictionary` for thread-safe in-memory storage
- Suitable for tens of thousands of entries
- For larger datasets, migrate to database-backed repository

### Pagination
- Default page size: 20 items
- Maximum page size: 100 items
- Use pagination for large result sets to avoid memory issues

### Filtering
- Filters are applied in memory
- Multiple filters can significantly reduce result set
- Use specific filters for better performance

## Testing

### Unit Tests
- `TokenRegistryServiceTests`: 8 tests covering validation, upsert, search
- `TokenRegistryRepositoryTests`: 9 tests covering CRUD, filtering, pagination
- All tests use NUnit framework with Moq for mocking

### Running Tests
```bash
cd BiatecTokensApi
dotnet test --filter "FullyQualifiedName~TokenRegistry"
```

### Test Coverage
- Repository CRUD operations
- Filtering by all supported criteria
- Pagination edge cases
- Validation rules
- Error handling

## Future Enhancements

### Planned Features
1. **External Registry Integration**: Add support for Vestige, CoinMarketCap, etc.
2. **Database Backend**: Migrate from in-memory to persistent storage
3. **Caching**: Add Redis for improved performance
4. **Webhooks**: Notify subscribers of registry updates
5. **Bulk Operations**: Support batch upserts
6. **Advanced Search**: Full-text search with Elasticsearch
7. **Analytics**: Registry statistics and trends

### Extensibility Points
- `IRegistryIngestionService.NormalizeTokenDataAsync()`: Add new source normalization
- `TokenRegistryService.ValidateTokenAsync()`: Extend validation rules
- Repository interface: Swap implementations without API changes

## Support and Troubleshooting

### Common Issues

**Issue**: Token not found by symbol
- **Solution**: Use chain parameter to disambiguate: `?chain=algorand-mainnet`

**Issue**: Ingestion creates duplicates
- **Solution**: Ingestion is idempotent by design; check logs for anomalies

**Issue**: Validation fails with required field error
- **Solution**: Ensure all required fields are provided in request

### Logging

All operations are logged with appropriate context:
- Info: Successful operations, query parameters
- Warning: Validation warnings, anomalies
- Error: Failures, exceptions

Check application logs for detailed information.

## API Stability

The registry API is designed for stability:
- Models use nullable types for optional fields
- Missing data represented explicitly with null rather than omitted
- New fields added as nullable to maintain backward compatibility
- Deprecations announced well in advance

## Security

### Authentication
- All endpoints require ARC-0014 authentication
- User address captured in CreatedBy field

### Input Sanitization
- All user inputs sanitized before logging to prevent log forging
- URL validation prevents malicious links
- Field length limits prevent DOS attacks

### Data Privacy
- No sensitive data stored in registry
- Compliance metadata does not include PII
- Audit logs use addresses, not personal identities

## Compliance Features

### Regulatory Framework Support
- MICA (Markets in Crypto-Assets Regulation)
- SEC Reg D
- FATF Guidelines
- Extensible for new frameworks

### Audit Trail
- All registry changes logged
- CreatedBy and UpdatedBy tracking
- Timestamps for all operations

### Reporting
- Compliance status aggregations
- Filter by regulatory framework
- Export capabilities (via existing audit endpoints)

## Integration Examples

### Frontend Discovery View
```javascript
// Fetch compliant ARC3 tokens
const response = await fetch('/api/v1/registry/tokens?standard=ARC3&complianceStatus=Compliant&page=1&pageSize=20', {
  headers: {
    'Authorization': `SigTx ${signedTransaction}`
  }
});
const data = await response.json();
// Display data.tokens in UI
```

### Compliance Dashboard
```javascript
// Get all compliance states
const filters = ['Compliant', 'Pending', 'NonCompliant'];
const results = await Promise.all(
  filters.map(status => 
    fetch(`/api/v1/registry/tokens?complianceStatus=${status}&pageSize=1`)
      .then(r => r.json())
      .then(d => ({ status, count: d.totalCount }))
  )
);
```

### Token Details Panel
```javascript
// Get complete token information
const response = await fetch(`/api/v1/registry/tokens/${tokenId}`, {
  headers: {
    'Authorization': `SigTx ${signedTransaction}`
  }
});
const data = await response.json();
if (data.found) {
  // Display token details, compliance, readiness
  renderTokenDetails(data.token);
}
```

## References

- **ARC-0014 Authentication**: https://github.com/algorandfoundation/ARCs/blob/main/ARCs/arc-0014.md
- **Token Standards**: See `Models/TokenStandards/TokenStandard.cs`
- **Compliance Models**: See `Models/Compliance/ComplianceMetadata.cs`
- **API Controllers**: `Controllers/TokenRegistryController.cs`

## Changelog

### Version 1.0 (2026-02-04)
- Initial implementation
- Support for ASA, ARC3, ARC19, ARC69, ARC200, ARC1400, ERC20
- In-memory repository with concurrent access
- Internal token ingestion from deployment records
- Full API endpoint suite with filtering and pagination
- Comprehensive test coverage (17 tests)

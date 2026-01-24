# RWA Whitelisting Rules API Documentation

## Overview

The RWA Whitelisting Rules API provides comprehensive management of whitelisting rules for Real World Asset (RWA) tokens, aligned with MICA (Markets in Crypto-Assets) regulatory requirements. This API enables token issuers to define, manage, and enforce compliance rules for their RWA tokens on Algorand networks (VOI, Aramid, and others).

## Business Value

### Regulatory Compliance
- **MICA Alignment**: Implements EU's Markets in Crypto-Assets regulation requirements
- **KYC/AML Enforcement**: Configurable KYC verification requirements with approved provider lists
- **Audit Trail**: Complete who/when/what tracking for regulatory reporting
- **Network-Specific Rules**: Different compliance requirements for different networks (VOI vs Aramid)

### Risk Mitigation
- **Legal Risk**: Prevents non-compliant token transfers that could result in regulatory fines
- **Operational Risk**: Automated rule enforcement reduces manual compliance checks
- **Reputational Risk**: Demonstrates commitment to regulatory compliance

### Market Enablement
- **Enterprise Ready**: Role-based access control for administrative operations
- **Multi-Network Support**: Rules can apply globally or to specific networks
- **Flexible Configuration**: Support for 6 rule types covering common compliance scenarios

## Rule Types

### 1. KYC Required (`KycRequired`)
Enforces KYC verification for whitelist entries.

**Configuration:**
- `KycMandatory` (bool): Whether KYC is mandatory
- `ApprovedKycProviders` (list): List of approved KYC provider names
- `ValidationMessage` (string): Custom error message when validation fails

**Use Case:** Require all participants on Aramid network to have KYC verification from Sumsub.

**Example:**
```json
{
  "assetId": 12345,
  "name": "Aramid KYC Required",
  "ruleType": "KycRequired",
  "network": "aramidmain-v1.0",
  "configuration": {
    "kycMandatory": true,
    "approvedKycProviders": ["Sumsub"],
    "validationMessage": "Aramid network requires Sumsub KYC verification"
  }
}
```

### 2. Role-Based Access (`RoleBasedAccess`)
Enforces minimum role requirements for whitelist entries.

**Configuration:**
- `MinimumRole` (enum): Admin or Operator

**Use Case:** Require Admin role for high-value RWA token holders.

**Example:**
```json
{
  "assetId": 12345,
  "name": "Admin Only",
  "ruleType": "RoleBasedAccess",
  "configuration": {
    "minimumRole": "Admin"
  }
}
```

### 3. Network-Specific (`NetworkSpecific`)
Enforces network-specific requirements.

**Configuration:**
- `NetworkRequirement` (string): Required network (e.g., "voimain-v1.0")

**Use Case:** Restrict token to specific blockchain network.

### 4. Expiration Required (`ExpirationRequired`)
Enforces expiration date requirements.

**Configuration:**
- `ExpirationMandatory` (bool): Whether expiration date is required
- `MaxValidityDays` (int): Maximum validity period in days

**Use Case:** Require periodic re-verification with max 365-day validity.

**Example:**
```json
{
  "assetId": 12345,
  "name": "Annual Re-verification",
  "ruleType": "ExpirationRequired",
  "configuration": {
    "expirationMandatory": true,
    "maxValidityDays": 365
  }
}
```

### 5. Status Validation (`StatusValidation`)
Enforces specific status requirements.

**Configuration:**
- `RequiredStatus` (enum): Active, Inactive, or Revoked

**Use Case:** Ensure only Active entries are validated.

### 6. Composite (`Composite`)
Combines multiple rules for complex compliance scenarios.

**Configuration:**
- `CompositeRuleIds` (list): List of rule IDs to combine

**Use Case:** Require both KYC verification AND Admin role for Aramid network.

**Example:**
```json
{
  "assetId": 12345,
  "name": "Aramid Full Compliance",
  "ruleType": "Composite",
  "network": "aramidmain-v1.0",
  "configuration": {
    "compositeRuleIds": ["kyc-rule-id", "role-rule-id"],
    "validationMessage": "Aramid requires KYC and Admin role"
  }
}
```

## API Endpoints

### Create Rule
`POST /api/v1/whitelist/rules`

Creates a new whitelisting rule.

**Authentication:** Required (ARC-0014)

**Request Body:**
```json
{
  "assetId": 12345,
  "name": "Rule Name",
  "description": "Optional description",
  "ruleType": "KycRequired",
  "priority": 100,
  "isEnabled": true,
  "network": "voimain-v1.0",
  "configuration": {
    "kycMandatory": true
  }
}
```

**Response:**
```json
{
  "success": true,
  "rule": {
    "id": "generated-rule-id",
    "assetId": 12345,
    "name": "Rule Name",
    "ruleType": "KycRequired",
    "priority": 100,
    "isEnabled": true,
    "createdBy": "CREATOR_ADDRESS",
    "createdAt": "2026-01-24T00:00:00Z"
  }
}
```

### Update Rule
`PUT /api/v1/whitelist/rules/{ruleId}`

Updates an existing rule. Only provided fields are updated.

**Authentication:** Required (ARC-0014)

### Get Rule
`GET /api/v1/whitelist/rules/{ruleId}`

Retrieves a specific rule by ID.

**Authentication:** Required (ARC-0014)

### List Rules
`GET /api/v1/whitelist/rules/asset/{assetId}`

Lists all rules for an asset with filtering and pagination.

**Query Parameters:**
- `ruleType` (optional): Filter by rule type
- `network` (optional): Filter by network
- `isEnabled` (optional): Filter by enabled status
- `page` (default: 1): Page number
- `pageSize` (default: 20, max: 100): Results per page

**Authentication:** Required (ARC-0014)

**Response:**
```json
{
  "success": true,
  "rules": [...],
  "totalCount": 25,
  "page": 1,
  "pageSize": 20,
  "totalPages": 2
}
```

### Delete Rule
`DELETE /api/v1/whitelist/rules/{ruleId}`

Deletes a rule. Does not affect existing whitelist entries.

**Authentication:** Required (ARC-0014)

### Apply Rule
`POST /api/v1/whitelist/rules/{ruleId}/apply`

Applies a rule to existing whitelist entries for validation.

**Authentication:** Required (ARC-0014)

**Request Body:**
```json
{
  "ruleId": "rule-id",
  "applyToExisting": true,
  "failOnError": false
}
```

**Response:**
```json
{
  "success": true,
  "entriesEvaluated": 100,
  "entriesPassed": 85,
  "entriesFailed": 15,
  "failedAddresses": ["ADDR1", "ADDR2", ...],
  "validationErrors": [
    {
      "ruleId": "rule-id",
      "ruleName": "KYC Required",
      "address": "ADDR1",
      "errorMessage": "KYC verification is required",
      "fieldName": "KycVerified"
    }
  ]
}
```

### Validate Against Rules
`POST /api/v1/whitelist/rules/validate`

Validates whitelist entries against all enabled rules.

**Authentication:** Required (ARC-0014)

**Request Body:**
```json
{
  "assetId": 12345,
  "address": "SPECIFIC_ADDRESS",  // Optional
  "ruleId": "specific-rule-id"     // Optional
}
```

## Network-Specific Guidelines

### VOI Network (`voimain-v1.0`)
- **KYC**: Recommended but not mandatory
- **Typical Use Case**: Community tokens, governance tokens
- **Rule Priority**: Lower priority (100-199)

### Aramid Network (`aramidmain-v1.0`)
- **KYC**: Mandatory for Active entries
- **Approved Providers**: Typically restricted list (e.g., Sumsub only)
- **Role Requirements**: Often requires Admin role for high-value assets
- **Rule Priority**: Higher priority (200-299)

### Global Rules (network: null)
- Apply to all networks
- Useful for baseline compliance requirements
- Network-specific rules take precedence

## Security Constraints

### Authentication
- All endpoints require ARC-0014 authentication
- User's Algorand address extracted from authentication token
- Address used for audit logging (createdBy, updatedBy, performedBy)

### Authorization
- Create/Update/Delete operations: Any authenticated user can manage rules for their tokens
- List/Get/Validate operations: Any authenticated user
- Future Enhancement: Token-specific admin roles

### Input Validation
- Rule configurations validated based on rule type
- KYC rules must specify KycMandatory or ApprovedKycProviders
- Role rules must specify MinimumRole
- Network rules must specify NetworkRequirement
- Expiration rules must specify ExpirationMandatory or MaxValidityDays
- Status rules must specify RequiredStatus
- Composite rules must specify at least one CompositeRuleId

### Audit Logging
- All rule operations (Create, Update, Delete, Apply) logged
- Audit logs include:
  - Rule ID and Asset ID
  - Action type
  - Performed by (user address)
  - Timestamp
  - Old state and new state (for updates)
  - Notes

## Integration Examples

### Example 1: Aramid Network Compliance
```javascript
// Step 1: Create KYC rule
const kycRule = await createRule({
  assetId: 12345,
  name: "Aramid KYC Required",
  ruleType: "KycRequired",
  network: "aramidmain-v1.0",
  priority: 200,
  configuration: {
    kycMandatory: true,
    approvedKycProviders: ["Sumsub"]
  }
});

// Step 2: Add whitelist entry
const entry = await addWhitelistEntry({
  assetId: 12345,
  address: "ALGORAND_ADDRESS...",
  network: "aramidmain-v1.0",
  kycVerified: true,
  kycProvider: "Sumsub",
  status: "Active"
});

// Step 3: Validate before transfer
const validation = await validateTransfer({
  assetId: 12345,
  fromAddress: "SENDER_ADDRESS...",
  toAddress: "RECEIVER_ADDRESS..."
});

if (validation.isAllowed) {
  // Execute blockchain transfer
} else {
  console.error("Transfer denied:", validation.denialReason);
}
```

### Example 2: Multi-Network Token with Different Rules
```javascript
// VOI Network: Relaxed rules
await createRule({
  assetId: 12345,
  name: "VOI Basic Compliance",
  ruleType: "StatusValidation",
  network: "voimain-v1.0",
  priority: 100,
  configuration: {
    requiredStatus: "Active"
  }
});

// Aramid Network: Strict rules
await createRule({
  assetId: 12345,
  name: "Aramid Strict Compliance",
  ruleType: "KycRequired",
  network: "aramidmain-v1.0",
  priority: 200,
  configuration: {
    kycMandatory: true,
    approvedKycProviders: ["Sumsub"]
  }
});

// Global Rule: Applies to all networks
await createRule({
  assetId: 12345,
  name: "Global Expiration Policy",
  ruleType: "ExpirationRequired",
  network: null,
  priority: 50,
  configuration: {
    maxValidityDays: 365
  }
});
```

## Testing

### Unit Tests (32 tests)
- Repository layer: CRUD operations, filtering, audit logging
- Service layer: Business logic, validation, rule application
- Controller layer: HTTP endpoints, authentication, error handling

### Integration Tests (5 tests)
- VOI network KYC enforcement
- Aramid network strict compliance
- Composite rules for multi-requirement scenarios
- Cross-network global rules
- Complete audit trail verification

**Run Tests:**
```bash
dotnet test --filter "FullyQualifiedName~WhitelistRules"
```

## Future Enhancements

1. **Persistent Storage**: Replace in-memory storage with database
2. **Token-Specific Authorization**: Verify user is token admin before allowing rule operations
3. **Rule Templates**: Pre-configured rule templates for common scenarios
4. **Webhook Notifications**: Real-time alerts for rule violations
5. **Batch Validation**: Validate multiple addresses in single request
6. **Rule Scheduling**: Enable/disable rules based on time schedules
7. **Advanced Composite Logic**: Support AND/OR logic for composite rules

## Support

For questions or issues:
- API Documentation: Available at `/swagger` endpoint
- Repository: https://github.com/scholtz/BiatecTokensApi
- Issue Tracking: GitHub Issues

## Related Documentation

- [WHITELIST_FEATURE.md](./WHITELIST_FEATURE.md) - Base whitelist functionality
- [RWA_WHITELIST_BUSINESS_VALUE.md](./RWA_WHITELIST_BUSINESS_VALUE.md) - Business case and ROI
- [MICA_COMPLIANCE_SIGNALS_API_ROADMAP.md](./MICA_COMPLIANCE_SIGNALS_API_ROADMAP.md) - Compliance roadmap

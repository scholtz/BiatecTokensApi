# Network-Aware Whitelist with Role-Based Access Control

## Overview

The RWA whitelist feature now supports network-specific validation rules for VOI and Aramid blockchains, along with role-based access control for enterprise compliance requirements.

## Network Field

The `Network` field identifies which blockchain network the token is deployed on. This enables network-specific compliance rules.

**Supported Networks:**
- `voimain-v1.0` - VOI mainnet
- `aramidmain-v1.0` - Aramid mainnet
- `mainnet-v1.0` - Algorand mainnet
- `testnet-v1.0` - Algorand testnet
- Other networks (no specific validation rules)

## Role-Based Access Control

Two roles are supported for whitelist management:

### Admin Role
- **Full Access**: Can perform all operations including revoking entries
- **Networks**: All networks
- **Use Case**: Token administrators, compliance officers

### Operator Role
- **Limited Access**: Can add and update entries, but cannot revoke
- **Networks**: All networks
- **Use Case**: Customer support, day-to-day operations

## Network-Specific Validation Rules

### VOI Network (`voimain-v1.0`)

#### Rule 1: KYC Verification Recommended
- **Status**: Warning only (not enforced)
- **Condition**: Active whitelist entries without KYC verification
- **Action**: Logs warning but allows operation to proceed
- **Rationale**: Encourages KYC compliance while maintaining flexibility

**Example:**
```json
{
  "assetId": 12345,
  "address": "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA",
  "status": "Active",
  "network": "voimain-v1.0",
  "kycVerified": false,
  "role": "Admin"
}
```
✅ **Result**: Allowed with warning logged

#### Rule 2: Operator Cannot Revoke
- **Status**: Enforced
- **Condition**: Operator role attempting to set status to Revoked
- **Action**: Operation denied
- **Rationale**: Revocation requires admin approval for audit purposes

**Example:**
```json
{
  "assetId": 12345,
  "address": "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA",
  "status": "Revoked",
  "network": "voimain-v1.0",
  "kycVerified": true,
  "role": "Operator"
}
```
❌ **Result**: Denied - "Operator role cannot revoke whitelist entries on VOI network. Admin privileges required."

### Aramid Network (`aramidmain-v1.0`)

#### Rule 1: KYC Verification Mandatory
- **Status**: Enforced
- **Condition**: Active whitelist entries must have KYC verification
- **Action**: Operation denied if KYC not verified
- **Rationale**: MICA compliance requirements for Aramid network

**Example (Invalid):**
```json
{
  "assetId": 12345,
  "address": "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA",
  "status": "Active",
  "network": "aramidmain-v1.0",
  "kycVerified": false,
  "role": "Admin"
}
```
❌ **Result**: Denied - "Aramid network requires KYC verification for Active whitelist entries."

**Example (Valid):**
```json
{
  "assetId": 12345,
  "address": "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA",
  "status": "Active",
  "network": "aramidmain-v1.0",
  "kycVerified": true,
  "kycProvider": "Sumsub",
  "role": "Admin"
}
```
✅ **Result**: Allowed

#### Rule 2: KYC Provider Required When Verified
- **Status**: Enforced
- **Condition**: When KYC is marked as verified, provider must be specified
- **Action**: Operation denied if provider missing
- **Rationale**: Audit trail requirement for MICA compliance

**Example:**
```json
{
  "assetId": 12345,
  "address": "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA",
  "status": "Active",
  "network": "aramidmain-v1.0",
  "kycVerified": true,
  "kycProvider": null,
  "role": "Admin"
}
```
❌ **Result**: Denied - "Aramid network requires KYC provider to be specified when KYC is verified."

#### Rule 3: Operator Cannot Revoke
- **Status**: Enforced
- **Condition**: Operator role attempting to set status to Revoked
- **Action**: Operation denied
- **Rationale**: Enhanced security for regulatory-critical Aramid network

**Example:**
```json
{
  "assetId": 12345,
  "address": "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA",
  "status": "Revoked",
  "network": "aramidmain-v1.0",
  "kycVerified": true,
  "kycProvider": "Sumsub",
  "role": "Operator"
}
```
❌ **Result**: Denied - "Operator role cannot revoke whitelist entries on Aramid network. Admin privileges required."

## API Endpoints

All existing whitelist endpoints now support Network and Role fields:

### Add Single Entry
```http
POST /api/v1/whitelist
Authorization: SigTx <signed-transaction>
Content-Type: application/json

{
  "assetId": 12345,
  "address": "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA",
  "status": "Active",
  "network": "aramidmain-v1.0",
  "kycVerified": true,
  "kycProvider": "Sumsub",
  "role": "Admin",
  "reason": "Accredited investor",
  "expirationDate": "2025-12-31T00:00:00Z"
}
```

### Bulk Add
```http
POST /api/v1/whitelist/bulk
Authorization: SigTx <signed-transaction>
Content-Type: application/json

{
  "assetId": 12345,
  "addresses": [
    "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA",
    "7ZUECA7HFLZTXENRV24SHLU4AVPUTMTTDUFUBNBD64C73F3UHRTHAIOF6Q"
  ],
  "status": "Active",
  "network": "voimain-v1.0",
  "kycVerified": true,
  "kycProvider": "Sumsub",
  "role": "Admin"
}
```

### List Entries
```http
GET /api/v1/whitelist/12345?status=Active&page=1&pageSize=20
Authorization: SigTx <signed-transaction>
```

Response includes Network and Role fields:
```json
{
  "success": true,
  "entries": [
    {
      "id": "uuid",
      "assetId": 12345,
      "address": "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA",
      "status": "Active",
      "network": "aramidmain-v1.0",
      "role": "Admin",
      "kycVerified": true,
      "kycProvider": "Sumsub",
      "createdBy": "ADMIN_ADDRESS",
      "createdAt": "2026-01-22T17:00:00Z"
    }
  ],
  "totalCount": 1,
  "page": 1,
  "pageSize": 20,
  "totalPages": 1
}
```

### Audit Log
```http
GET /api/v1/whitelist/12345/audit-log?fromDate=2026-01-01&toDate=2026-01-31
Authorization: SigTx <signed-transaction>
```

Response includes Network and Role in audit entries:
```json
{
  "success": true,
  "entries": [
    {
      "id": "uuid",
      "assetId": 12345,
      "address": "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA",
      "actionType": "Add",
      "performedBy": "ADMIN_ADDRESS",
      "performedAt": "2026-01-22T17:00:00Z",
      "oldStatus": null,
      "newStatus": "Active",
      "network": "aramidmain-v1.0",
      "role": "Admin"
    }
  ],
  "totalCount": 1,
  "page": 1,
  "pageSize": 50,
  "totalPages": 1
}
```

## Backwards Compatibility

All changes are backwards compatible:
- Network and Role fields are **optional** (nullable)
- Existing whitelist entries without Network continue to work
- When Network is not specified, no network-specific validation is applied
- Default Role is Admin if not specified

## Best Practices

1. **Always Specify Network**: For new entries, always include the network field for proper validation
2. **Use Appropriate Role**: Assign Operator role for day-to-day operations, Admin for sensitive operations
3. **KYC Documentation**: Always specify KYC provider when KYC is verified, especially on Aramid
4. **Audit Trail**: Review audit logs regularly for compliance reporting
5. **Test on Testnet**: Test network validation rules on testnet before production deployment

## Migration Guide

For existing whitelist entries without Network field:
1. Entries continue to work without modification
2. No network-specific validation is applied
3. To add network tracking:
   - Update entries via POST /api/v1/whitelist (existing address)
   - Specify network field in update request
   - Entry will be validated against network rules

## Error Handling

Network validation errors return 400 Bad Request with clear error messages:

```json
{
  "success": false,
  "errorMessage": "Aramid network requires KYC verification for Active whitelist entries. Address: VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA"
}
```

Common error messages:
- `"Operator role cannot revoke whitelist entries on [network]. Admin privileges required."`
- `"[network] requires KYC verification for Active whitelist entries."`
- `"[network] requires KYC provider to be specified when KYC is verified."`
- `"Invalid Algorand address format: [address]"`

## Testing

Run network validation tests:
```bash
dotnet test BiatecTokensTests --filter "FullyQualifiedName~WhitelistServiceTests"
```

All 30 whitelist service tests should pass, including 9 network validation tests.

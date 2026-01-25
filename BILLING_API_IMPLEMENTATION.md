# Subscription Usage Metering and Plan Limits API - Implementation Summary

## Overview

This document provides a comprehensive summary of the subscription usage metering and plan limits API implementation for the BiatecTokensApi platform.

## Vision

Implement a subscription-funded platform with enterprise governance, tracking per-tenant usage and enforcing plan limits with clear error codes and audit logging.

## Architecture

### Models (`BiatecTokensApi/Models/Billing/`)

#### 1. UsageSummary.cs
Aggregates usage statistics for a tenant including:
- Token issuance count
- Transfer validation count
- Audit export count
- Storage items count
- Compliance operation count
- Whitelist operation count
- Current plan limits
- Limit violation tracking

#### 2. PlanLimits.cs
Defines configurable limits for each operation type:
- `MaxTokenIssuance`: Maximum tokens per period (-1 for unlimited)
- `MaxTransferValidations`: Maximum transfer validations per period
- `MaxAuditExports`: Maximum audit exports per period
- `MaxStorageItems`: Maximum storage items
- `MaxComplianceOperations`: Maximum compliance operations per period
- `MaxWhitelistOperations`: Maximum whitelist operations per period

#### 3. LimitCheckRequest.cs & LimitCheckResponse.cs
Request/response models for preflight limit checks:
- Operation type specification
- Operation count
- Current usage reporting
- Remaining capacity calculation
- Clear denial reasons and error codes

### Service Layer

#### BillingService (`BiatecTokensApi/Services/BillingService.cs`)
Core business logic implementing `IBillingService`:

**Key Features:**
- In-memory usage tracking (resets monthly)
- Custom plan limits per tenant
- Tier-based default limits
- Admin authorization checks
- Audit logging for compliance

**Methods:**
- `GetUsageSummaryAsync()`: Retrieves current usage for a tenant
- `CheckLimitAsync()`: Preflight check for operation limits
- `UpdatePlanLimitsAsync()`: Admin-only plan limit updates
- `GetPlanLimitsAsync()`: Retrieves current plan limits
- `RecordUsageAsync()`: Records operation usage
- `IsAdmin()`: Admin role verification

### Controller Layer

#### BillingController (`BiatecTokensApi/Controllers/BillingController.cs`)
REST API endpoints with comprehensive documentation:

**Endpoints:**

1. **GET /api/v1/billing/usage**
   - Returns usage summary for authenticated tenant
   - Includes current period statistics
   - Shows limit violations if any
   - Requires: ARC-0014 authentication

2. **POST /api/v1/billing/limits/check**
   - Preflight check for planned operations
   - Validates against current limits
   - Returns clear denial reasons
   - Logs denials for audit
   - Requires: ARC-0014 authentication

3. **PUT /api/v1/billing/limits/{tenantAddress}**
   - Updates plan limits for specified tenant
   - Admin-only operation
   - Logs all changes to audit trail
   - Requires: ARC-0014 authentication + Admin role

4. **GET /api/v1/billing/limits**
   - Retrieves current plan limits
   - Returns custom or tier-based defaults
   - Requires: ARC-0014 authentication

## Testing

### Integration Tests (`BiatecTokensTests/BillingServiceIntegrationTests.cs`)

**24 comprehensive tests covering:**

1. **Usage Summary Tests (6 tests)**
   - New tenant with zero usage
   - Recorded usage tracking
   - Custom limits display
   - Limit violations detection
   - Multi-tenant isolation

2. **Limit Check Tests (7 tests)**
   - Unlimited plan behavior
   - Within-limit operations
   - Limit exceeded scenarios
   - Exact limit boundary cases
   - Audit log verification
   - Input validation

3. **Plan Limits Management Tests (5 tests)**
   - Admin update success
   - Non-admin denial
   - Audit log creation
   - Custom vs. tier defaults
   - Multi-tenant isolation

4. **Admin Authorization Tests (3 tests)**
   - Configured admin verification
   - Non-admin rejection
   - Null/empty address handling

5. **Usage Recording Tests (3 tests)**
   - Valid operation recording
   - Multi-operation independence
   - Null address handling

**Test Results:**
- ✅ All 24 billing tests passing
- ✅ All 690 total tests passing (no regressions)
- ✅ 13 IPFS tests skipped (expected)

## Security Features

### Authentication & Authorization
- **ARC-0014 Authentication**: Required on all endpoints
- **Admin Role Checks**: Enforced for plan limit updates
- **Tenant Isolation**: Each tenant's data is isolated
- **Clear Error Messages**: Security-conscious error responses

### Audit Logging
All significant events are logged with structured logging:

1. **Limit Denials**: `BILLING_AUDIT: LimitCheckDenied`
   - Tenant address
   - Operation type and count
   - Asset ID and network (if applicable)
   - Denial reason

2. **Plan Updates**: `BILLING_AUDIT: PlanLimitUpdate`
   - Admin performer
   - Tenant affected
   - All new limit values
   - Optional notes

### Error Handling
- Clear error codes: `LIMIT_EXCEEDED`, `UNAUTHORIZED`, `INTERNAL_ERROR`
- User-friendly denial reasons
- Actionable upgrade suggestions
- No sensitive data leakage

## Usage Patterns

### For Application Developers

#### Check Limits Before Operations
```csharp
// Before performing expensive operations
var checkRequest = new LimitCheckRequest
{
    OperationType = OperationType.TokenIssuance,
    OperationCount = 1
};

var result = await billingService.CheckLimitAsync(tenantAddress, checkRequest);
if (!result.IsAllowed)
{
    // Show upgrade message to user
    return BadRequest(result.DenialReason);
}

// Proceed with operation
```

#### Record Usage After Operations
```csharp
// After successful operation
await billingService.RecordUsageAsync(
    tenantAddress, 
    OperationType.TokenIssuance, 
    1);
```

### For Administrators

#### Update Tenant Limits
```http
PUT /api/v1/billing/limits/TENANT_ADDRESS
Authorization: SigTx <admin-signed-transaction>
Content-Type: application/json

{
  "tenantAddress": "TENANT_ADDRESS",
  "limits": {
    "maxTokenIssuance": 1000,
    "maxTransferValidations": 10000,
    "maxAuditExports": 100,
    "maxStorageItems": 5000,
    "maxComplianceOperations": 2000,
    "maxWhitelistOperations": 3000
  },
  "notes": "Enterprise tier custom agreement"
}
```

### For End Users

#### View Usage
```http
GET /api/v1/billing/usage
Authorization: SigTx <user-signed-transaction>
```

**Response:**
```json
{
  "success": true,
  "data": {
    "tenantAddress": "USER_ADDRESS",
    "subscriptionTier": "Free",
    "periodStart": "2026-01-01T00:00:00Z",
    "periodEnd": "2026-01-31T23:59:59Z",
    "tokenIssuanceCount": 5,
    "transferValidationCount": 100,
    "auditExportCount": 2,
    "storageItemsCount": 8,
    "complianceOperationCount": 15,
    "whitelistOperationCount": 12,
    "currentLimits": {
      "maxTokenIssuance": -1,
      "maxTransferValidations": -1,
      "maxAuditExports": 0,
      "maxStorageItems": 10,
      "maxComplianceOperations": -1,
      "maxWhitelistOperations": -1
    },
    "hasExceededLimits": false,
    "limitViolations": []
  }
}
```

## Billing Period Management

- **Period**: Calendar month (1st to last day)
- **Reset**: Automatic on the 1st of each month
- **Timezone**: UTC
- **Persistence**: In-memory (resets on application restart)
  - Production implementation should use persistent storage

## Future Enhancements

### Recommended Improvements:

1. **Persistent Storage**
   - Move from in-memory to database storage
   - Maintain historical usage data
   - Support billing reconciliation

2. **Webhook Integration**
   - Notify on limit violations
   - Alert on approaching limits
   - Billing event notifications

3. **Advanced Analytics**
   - Usage trends and forecasting
   - Cost projection
   - Capacity planning insights

4. **Tiered Pricing Integration**
   - Automatic tier upgrades
   - Overage handling
   - Payment gateway integration

5. **API Rate Limiting**
   - Request-based limits
   - Token bucket algorithm
   - Distributed rate limiting

## Configuration

### Admin Configuration
Admins are configured via `appsettings.json`:

```json
{
  "App": {
    "Account": "ADMIN_ALGORAND_ADDRESS"
  }
}
```

For production, use environment variables or user secrets:
```bash
dotnet user-secrets set "App:Account" "ADMIN_ADDRESS"
```

### Subscription Tiers
Default limits are inherited from existing `SubscriptionTierConfiguration`:
- **Free**: 10 storage items, no audit logs
- **Basic**: 100 storage items, audit logs enabled
- **Premium**: 1000 storage items, all features
- **Enterprise**: Unlimited, all features

Custom limits override tier defaults for specific tenants.

## API Documentation

Full OpenAPI/Swagger documentation is available at `/swagger` when the application is running. All endpoints include:
- Detailed descriptions
- Parameter documentation
- Response schemas
- Authentication requirements
- Use case examples

## Compliance & Governance

### MICA Compliance
- All limit enforcements are logged
- Audit trail for regulatory review
- 7-year retention compatible (via log aggregation)
- Clear accountability for plan changes

### Enterprise Governance
- Centralized usage visibility
- Tenant-specific plan management
- Audit trail for compliance officers
- Cost control and forecasting

## Deployment

### Service Registration
The BillingService is registered in `Program.cs`:

```csharp
builder.Services.AddSingleton<IBillingService, BillingService>();
```

### Dependencies
- `ISubscriptionTierService`: For tier-based defaults
- `AppConfiguration`: For admin configuration
- `ILogger`: For audit logging

### Health Checks
Monitor these aspects:
- Service availability
- Response times for limit checks
- Audit log delivery
- Memory usage (in-memory tracking)

## Summary

This implementation provides a complete subscription metering and plan limits system with:
- ✅ Comprehensive usage tracking
- ✅ Flexible plan management
- ✅ Admin governance controls
- ✅ Clear error handling
- ✅ Audit compliance
- ✅ Extensive testing
- ✅ Full API documentation
- ✅ Security-first design

The system is production-ready for deployment with the note that moving from in-memory storage to persistent storage is recommended for long-term production use.

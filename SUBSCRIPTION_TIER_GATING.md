# Subscription Tier Gating for RWA Compliance

## Overview

The BiatecTokensApi implements subscription-based tier gating for whitelist operations to enable tiered pricing and usage-based billing for RWA (Real World Asset) compliance features. This system ensures that users can only access features and capacities appropriate to their subscription level while maintaining MICA regulatory compliance.

## Subscription Tiers

### Free Tier
- **Max Addresses Per Asset**: 10
- **Bulk Operations**: ❌ Not available
- **Audit Log Access**: ❌ Not available
- **Transfer Validation**: ✅ Available
- **Use Case**: Testing and small deployments

### Basic Tier
- **Max Addresses Per Asset**: 100
- **Bulk Operations**: ❌ Not available
- **Audit Log Access**: ✅ Available
- **Transfer Validation**: ✅ Available
- **Use Case**: Small to medium RWA token deployments

### Premium Tier
- **Max Addresses Per Asset**: 1,000
- **Bulk Operations**: ✅ Available
- **Audit Log Access**: ✅ Available
- **Transfer Validation**: ✅ Available
- **Use Case**: Larger institutional deployments

### Enterprise Tier
- **Max Addresses Per Asset**: ♾️ Unlimited
- **Bulk Operations**: ✅ Available
- **Audit Log Access**: ✅ Available
- **Transfer Validation**: ✅ Available
- **Use Case**: Large-scale enterprise deployments

## API Integration

### Tier Enforcement Points

#### 1. Adding Single Whitelist Entry
**Endpoint**: `POST /api/v1/whitelist`

When adding a single address to the whitelist, the system:
1. Checks the user's subscription tier
2. Counts existing whitelist entries for the asset
3. Validates if adding one more entry would exceed the tier limit
4. Returns an error if the limit would be exceeded

**Example Error Response (Free Tier Exceeded)**:
```json
{
  "success": false,
  "errorMessage": "Subscription tier 'Free' limit exceeded. Current: 10, Attempting to add: 1, Max allowed: 10. Please upgrade your subscription to add more addresses."
}
```

#### 2. Bulk Adding Whitelist Entries
**Endpoint**: `POST /api/v1/whitelist/bulk`

Bulk operations have two-level enforcement:
1. **Feature Gate**: Checks if bulk operations are enabled for the tier
2. **Capacity Gate**: Validates that the bulk addition won't exceed tier limits

**Example Error Response (Bulk Not Available)**:
```json
{
  "success": false,
  "errorMessage": "Bulk operations are not available in your subscription tier. Please upgrade to Premium or Enterprise tier."
}
```

**Example Error Response (Bulk Capacity Exceeded)**:
```json
{
  "success": false,
  "errorMessage": "Subscription tier 'Premium' limit exceeded. Current: 995, Attempting to add: 10, Max allowed: 1000. Please upgrade your subscription to add more addresses."
}
```

#### 3. Audit Log Access
**Endpoint**: `GET /api/v1/whitelist/{assetId}/audit-log`

Audit log access is restricted to Basic tier and above. Free tier users attempting to access audit logs should receive appropriate guidance to upgrade.

**Note**: Current implementation allows all tiers to access audit logs through the API, but this can be enforced by checking `IsAuditLogEnabledAsync()` before returning results.

## Implementation Details

### Service Layer

#### ISubscriptionTierService Interface
Provides methods for tier validation and feature gating:

```csharp
public interface ISubscriptionTierService
{
    Task<SubscriptionTier> GetUserTierAsync(string userAddress);
    Task<SubscriptionTierValidationResult> ValidateOperationAsync(
        string userAddress, ulong assetId, int currentCount, int additionalCount = 1);
    Task<bool> IsBulkOperationEnabledAsync(string userAddress);
    Task<bool> IsAuditLogEnabledAsync(string userAddress);
    SubscriptionTierLimits GetTierLimits(SubscriptionTier tier);
    Task<int> GetRemainingCapacityAsync(string userAddress, int currentCount);
}
```

#### SubscriptionTierService Implementation
The service uses an in-memory `ConcurrentDictionary` to store user tier assignments. In production, this should be replaced with a persistent storage backend (database, cache, etc.) without API changes.

**Default Behavior**: Users without an assigned tier default to the Free tier.

### Integration with WhitelistService

The `WhitelistService` has been updated to:
1. Accept `ISubscriptionTierService` dependency via constructor injection
2. Validate tier limits before adding new whitelist entries
3. Check bulk operation permissions before processing bulk requests
4. Return clear, actionable error messages when tier limits are exceeded

### Repository Layer

Added `GetEntriesCountAsync()` method to efficiently count whitelist entries per asset:

```csharp
Task<int> GetEntriesCountAsync(ulong assetId);
```

## Error Messages

All tier-related error messages follow a consistent pattern:
- Clear identification of the subscription tier
- Current count vs. max allowed
- Actionable guidance (upgrade subscription)
- No technical jargon or internal details

### Examples

**Capacity Limit Exceeded**:
```
Subscription tier 'Free' limit exceeded. Current: 10, Attempting to add: 1, Max allowed: 10. Please upgrade your subscription to add more addresses.
```

**Feature Not Available**:
```
Bulk operations are not available in your subscription tier. Please upgrade to Premium or Enterprise tier.
```

## Configuration

### Tier Limits Configuration
Tier limits are defined in `SubscriptionTierConfiguration.TierLimits` dictionary. To modify limits:

1. Edit `BiatecTokensApi/Models/Subscription/SubscriptionTier.cs`
2. Update the `TierLimits` dictionary initialization
3. Rebuild and redeploy

**Important**: Tier limit changes affect all users immediately. Consider migration strategies for existing users.

### Dependency Injection
The subscription tier service is registered as a singleton in `Program.cs`:

```csharp
builder.Services.AddSingleton<ISubscriptionTierService, SubscriptionTierService>();
```

## Testing

### Test Coverage
The implementation includes 19 comprehensive tests covering:
- Tier configuration validation (4 tests)
- Subscription tier service functionality (9 tests)
- Whitelist service integration (6 tests)

**Test File**: `BiatecTokensTests/SubscriptionTierGatingTests.cs`

### Running Tests
```bash
# Run all subscription tier tests
dotnet test --filter "FullyQualifiedName~SubscriptionTierGatingTests"

# Run all tests
dotnet test
```

## Migration from In-Memory to Persistent Storage

To migrate tier assignments to a database:

1. **Create a database table**:
```sql
CREATE TABLE UserSubscriptionTiers (
    UserAddress VARCHAR(58) PRIMARY KEY,
    Tier INT NOT NULL,
    AssignedAt DATETIME NOT NULL,
    ExpiresAt DATETIME NULL
);
```

2. **Update SubscriptionTierService**:
   - Replace `ConcurrentDictionary` with database context
   - Implement `GetUserTierAsync()` to query database
   - Add `SetUserTier()` to insert/update database records
   - Consider caching for performance

3. **No API changes required**: The interface remains the same

## Admin Operations

### Setting User Tiers
The `SubscriptionTierService` includes a `SetUserTier()` method for administrative tier management:

```csharp
// Example: Upgrade user to Premium tier
_tierService.SetUserTier("VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA", SubscriptionTier.Premium);
```

**Note**: This method is synchronous and operates in-memory. For production, expose this through an admin API endpoint with proper authentication and authorization.

### Recommended Admin API Endpoints
Consider implementing these endpoints for tier management:

```
POST   /api/v1/admin/users/{userAddress}/tier   - Set user's subscription tier
GET    /api/v1/admin/users/{userAddress}/tier   - Get user's current tier
GET    /api/v1/admin/users/{userAddress}/usage  - Get user's usage statistics
POST   /api/v1/admin/tiers/limits               - Update tier limits (requires restart)
```

## Security Considerations

### Authentication
All whitelist operations require ARC-0014 authentication. The authenticated user's address is used for tier validation.

### Authorization
- Tier assignments should only be modifiable by administrators
- Consider role-based access control (RBAC) for admin operations
- Log all tier assignment changes for audit purposes

### Validation
- User addresses are validated before tier operations
- Empty or null addresses default to Free tier
- Tier validation occurs before any state changes

## Best Practices

### For API Consumers
1. **Check tier limits before operations**: Use remaining capacity checks to provide proactive UI feedback
2. **Handle tier errors gracefully**: Display upgrade prompts instead of generic error messages
3. **Cache tier information**: Minimize API calls by caching user tier locally
4. **Implement retry logic**: For transient failures, not for tier limit errors

### For Administrators
1. **Monitor tier usage**: Track how many users are hitting tier limits
2. **Plan capacity**: Provision infrastructure based on tier distribution
3. **Communicate changes**: Notify users before tier limit changes
4. **Audit regularly**: Review tier assignments and usage patterns

### For Developers
1. **Test all tiers**: Ensure functionality works across all subscription tiers
2. **Consistent error messages**: Follow the established error message patterns
3. **Document tier requirements**: Clearly document which features require which tiers
4. **Performance considerations**: Count operations can be expensive at scale

## Troubleshooting

### Issue: Users can't add addresses despite being under the limit
**Cause**: Tier validation checks before adding, but another process may have added entries concurrently.

**Solution**: Implement optimistic locking or retry logic.

### Issue: Tier limits not being enforced
**Cause**: Tier service not properly injected or default tier being used.

**Solution**: 
1. Verify DI registration in `Program.cs`
2. Check user tier assignment
3. Ensure `GetUserTierAsync()` is being called

### Issue: Error messages not showing tier information
**Cause**: Tier validation result not being used correctly.

**Solution**: Always use `tierValidation.DenialReason` for error messages.

## Roadmap

### Planned Enhancements
- [ ] Persistent storage backend for tier assignments
- [ ] Admin API endpoints for tier management
- [ ] Usage analytics and reporting dashboard
- [ ] Tier expiration and renewal workflows
- [ ] Grace period when tier limits are exceeded
- [ ] Per-user usage quotas independent of tier
- [ ] Tier-based rate limiting
- [ ] Tiered pricing calculator API

### Future Considerations
- **Dynamic Tier Limits**: Allow per-user custom limits
- **Tier History**: Track tier changes over time
- **Usage Predictions**: Alert users before hitting limits
- **Auto-upgrade**: Automatic tier upgrades based on usage

## Related Documentation
- [WHITELIST_FEATURE.md](./WHITELIST_FEATURE.md) - Whitelist API documentation
- [COMPLIANCE_API.md](./COMPLIANCE_API.md) - Compliance metadata API
- [SUBSCRIPTION_METERING.md](./SUBSCRIPTION_METERING.md) - Metering events for billing

## Support
For questions or issues related to subscription tier gating, please:
- Check this documentation first
- Review test cases in `SubscriptionTierGatingTests.cs`
- Contact the development team
- File an issue on GitHub

---

**Version**: 1.0.0  
**Last Updated**: 2026-01-22  
**Author**: Biatec Development Team

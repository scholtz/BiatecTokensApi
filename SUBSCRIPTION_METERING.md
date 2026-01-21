# Subscription Metering Documentation

## Overview

The BiatecTokensApi implements subscription metering for compliance metadata and whitelist operations. This metering system emits structured log events that can be consumed by monitoring and analytics systems for billing and usage tracking purposes.

## Metering Events

Metering events are emitted as structured log entries with the prefix `METERING_EVENT:` and contain all necessary information for billing analytics.

### Event Schema

Each metering event contains the following fields:

| Field | Type | Description | Required |
|-------|------|-------------|----------|
| `EventId` | `string` (GUID) | Unique identifier for the metering event | Yes |
| `Timestamp` | `DateTime` (UTC) | When the operation occurred | Yes |
| `Category` | `MeteringCategory` enum | Category of operation (Compliance or Whitelist) | Yes |
| `OperationType` | `MeteringOperationType` enum | Type of operation performed | Yes |
| `AssetId` | `ulong` | Asset ID associated with the operation | Yes |
| `Network` | `string` | Network where the operation occurred (e.g., "voimain", "aramidmain", "testnet") | Optional |
| `PerformedBy` | `string` | Algorand address of the user who performed the operation | Optional |
| `ItemCount` | `int` | Number of items affected by the operation (default: 1) | Yes |
| `Metadata` | `Dictionary<string, string>` | Additional metadata about the operation | Optional |

### Event Categories

#### MeteringCategory Enum

- **Compliance** - Compliance metadata operations
- **Whitelist** - Whitelist operations

#### MeteringOperationType Enum

- **Upsert** - Create or update operation (compliance metadata)
- **Delete** - Delete operation (compliance metadata)
- **Add** - Add operation (single whitelist entry)
- **Update** - Update operation (single whitelist entry)
- **Remove** - Remove operation (single whitelist entry)
- **BulkAdd** - Bulk add operation (multiple whitelist entries)

## Metered Operations

### Compliance Operations

#### Upsert Compliance Metadata
- **Endpoint**: `POST /api/v1/compliance`
- **Category**: `Compliance`
- **OperationType**: `Upsert`
- **ItemCount**: Always 1
- **Network**: Provided in the request
- **PerformedBy**: Authenticated user's address
- **When Emitted**: On successful upsert of compliance metadata

#### Delete Compliance Metadata
- **Endpoint**: `DELETE /api/v1/compliance/{assetId}`
- **Category**: `Compliance`
- **OperationType**: `Delete`
- **ItemCount**: Always 1
- **Network**: Not available (set to null)
- **PerformedBy**: Not available (set to null)
- **When Emitted**: On successful deletion of compliance metadata

### Whitelist Operations

#### Add Whitelist Entry
- **Endpoint**: `POST /api/v1/whitelist`
- **Category**: `Whitelist`
- **OperationType**: `Add` (for new entries) or `Update` (for existing entries)
- **ItemCount**: Always 1
- **Network**: Not available (set to null)
- **PerformedBy**: Authenticated user's address
- **When Emitted**: On successful addition or update of a whitelist entry

#### Remove Whitelist Entry
- **Endpoint**: `DELETE /api/v1/whitelist`
- **Category**: `Whitelist`
- **OperationType**: `Remove`
- **ItemCount**: Always 1
- **Network**: Not available (set to null)
- **PerformedBy**: User who last modified the entry
- **When Emitted**: On successful removal of a whitelist entry

#### Bulk Add Whitelist Entries
- **Endpoint**: `POST /api/v1/whitelist/bulk`
- **Category**: `Whitelist`
- **OperationType**: `BulkAdd`
- **ItemCount**: Number of successfully added/updated entries
- **Network**: Not available (set to null)
- **PerformedBy**: Authenticated user's address
- **When Emitted**: On successful bulk operation (only if at least one entry succeeds)
- **Note**: ItemCount reflects only successful operations, not failed ones

## Log Format

Metering events are emitted as structured logs with the following format:

```
METERING_EVENT: {EventId} | Category: {Category} | Operation: {OperationType} | AssetId: {AssetId} | Network: {Network} | ItemCount: {ItemCount} | PerformedBy: {PerformedBy} | Timestamp: {Timestamp} | Metadata: {Metadata}
```

### Example Log Entries

#### Compliance Upsert
```
METERING_EVENT: 550e8400-e29b-41d4-a716-446655440000 | Category: Compliance | Operation: Upsert | AssetId: 12345 | Network: voimain | ItemCount: 1 | PerformedBy: VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA | Timestamp: 2024-01-21T10:30:00.000Z | Metadata: {}
```

#### Whitelist Add
```
METERING_EVENT: 660e9511-f30c-52e5-b827-557766551111 | Category: Whitelist | Operation: Add | AssetId: 67890 | Network: unknown | ItemCount: 1 | PerformedBy: VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA | Timestamp: 2024-01-21T10:35:00.000Z | Metadata: {}
```

#### Whitelist Bulk Add
```
METERING_EVENT: 770f0622-041d-63f6-c938-668877662222 | Category: Whitelist | Operation: BulkAdd | AssetId: 11111 | Network: unknown | ItemCount: 50 | PerformedBy: VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA | Timestamp: 2024-01-21T10:40:00.000Z | Metadata: {}
```

## Integration with Analytics Systems

### Log Aggregation

The metering events can be consumed by:
- **Elasticsearch/Kibana**: Parse structured logs and create dashboards
- **Splunk**: Index logs and create billing reports
- **CloudWatch/Application Insights**: Query logs for usage metrics
- **Custom analytics pipelines**: Parse the structured log format

### Billing Calculations

To calculate billing:

1. **Filter logs** by `METERING_EVENT` prefix
2. **Parse** the structured log data
3. **Group by**:
   - `Category` - Separate compliance and whitelist operations
   - `OperationType` - Different pricing for different operations
   - `AssetId` - Per-token billing
   - `Network` - Network-specific pricing
   - `PerformedBy` - User-level billing
4. **Sum** `ItemCount` for total operations
5. **Apply pricing rules** based on operation types

### Example Queries

#### Count compliance operations by user
```
Filter: METERING_EVENT AND Category: Compliance
Group by: PerformedBy
Aggregate: SUM(ItemCount)
```

#### Count bulk whitelist operations
```
Filter: METERING_EVENT AND OperationType: BulkAdd
Aggregate: SUM(ItemCount)
```

#### Calculate monthly billing per asset
```
Filter: METERING_EVENT AND Timestamp >= startOfMonth
Group by: AssetId, Category, OperationType
Aggregate: SUM(ItemCount)
```

## Error Handling

### When Events Are NOT Emitted

Metering events are not emitted in the following scenarios:

1. **Operation Failure**: If the operation fails (e.g., validation error, database error)
2. **Bulk Operation Total Failure**: If all items in a bulk operation fail
3. **Null Event**: If a null metering event is passed to the service

### Logging Behavior

- **Success**: Emits an `Information` level log with the metering event
- **Null Event**: Emits a `Warning` level log indicating a null event was attempted

## Configuration

No additional configuration is required. The metering service is automatically registered in the dependency injection container and injected into the compliance and whitelist services.

### Service Registration

```csharp
builder.Services.AddSingleton<ISubscriptionMeteringService, SubscriptionMeteringService>();
```

## API Reference

### ISubscriptionMeteringService Interface

```csharp
public interface ISubscriptionMeteringService
{
    void EmitMeteringEvent(SubscriptionMeteringEvent meteringEvent);
}
```

### SubscriptionMeteringEvent Model

```csharp
public class SubscriptionMeteringEvent
{
    public string EventId { get; set; } = Guid.NewGuid().ToString();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? Network { get; set; }
    public ulong AssetId { get; set; }
    public MeteringOperationType OperationType { get; set; }
    public MeteringCategory Category { get; set; }
    public string? PerformedBy { get; set; }
    public int ItemCount { get; set; } = 1;
    public Dictionary<string, string>? Metadata { get; set; }
}
```

## Security Considerations

- **No Sensitive Data**: Metering events do not contain sensitive information like mnemonics or private keys
- **User Privacy**: Only Algorand public addresses are logged (already public information)
- **Audit Trail**: Metering events can serve as an audit trail for compliance and security purposes
- **Log Retention**: Follow your organization's log retention policies

## Testing

Comprehensive unit tests are available in:
- `SubscriptionMeteringServiceTests.cs` - Tests for the metering service
- `ComplianceServiceTests.cs` - Tests for compliance metering hooks
- `WhitelistServiceTests.cs` - Tests for whitelist metering hooks

Run tests with:
```bash
dotnet test --filter "FullyQualifiedName~MeteringTests"
```

## Future Enhancements

Potential improvements to the metering system:

1. **Real-time Analytics**: Direct integration with analytics platforms
2. **Rate Limiting**: Use metering data for rate limiting
3. **Quota Management**: Enforce usage quotas based on subscription tiers
4. **Enhanced Metadata**: Include request IP, user agent, or other context
5. **Event Batching**: Batch events for high-volume scenarios
6. **Cost Attribution**: Tag operations with cost centers or departments

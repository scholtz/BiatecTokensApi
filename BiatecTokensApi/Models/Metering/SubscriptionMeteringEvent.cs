namespace BiatecTokensApi.Models.Metering
{
    /// <summary>
    /// Represents a subscription metering event for billing and analytics purposes
    /// </summary>
    /// <remarks>
    /// This model captures metered operations for compliance metadata and whitelist changes.
    /// Events are emitted to enable billing analytics and usage tracking.
    /// </remarks>
    public class SubscriptionMeteringEvent
    {
        /// <summary>
        /// Unique identifier for the metering event
        /// </summary>
        public string EventId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Timestamp when the event occurred
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Network where the operation occurred (e.g., "voimain", "aramidmain", "testnet")
        /// </summary>
        public string? Network { get; set; }

        /// <summary>
        /// Asset ID associated with the operation
        /// </summary>
        public ulong AssetId { get; set; }

        /// <summary>
        /// Type of operation performed
        /// </summary>
        public MeteringOperationType OperationType { get; set; }

        /// <summary>
        /// Category of the operation (Compliance or Whitelist)
        /// </summary>
        public MeteringCategory Category { get; set; }

        /// <summary>
        /// User who performed the operation
        /// </summary>
        public string? PerformedBy { get; set; }

        /// <summary>
        /// Number of items affected by the operation (e.g., count of addresses in bulk operations)
        /// </summary>
        public int ItemCount { get; set; } = 1;

        /// <summary>
        /// Additional metadata about the operation
        /// </summary>
        public Dictionary<string, string>? Metadata { get; set; }
    }

    /// <summary>
    /// Categories of metered operations
    /// </summary>
    public enum MeteringCategory
    {
        /// <summary>
        /// Compliance metadata operations
        /// </summary>
        Compliance,

        /// <summary>
        /// Whitelist operations
        /// </summary>
        Whitelist
    }

    /// <summary>
    /// Types of metered operations
    /// </summary>
    public enum MeteringOperationType
    {
        /// <summary>
        /// Create or update operation
        /// </summary>
        Upsert,

        /// <summary>
        /// Delete operation
        /// </summary>
        Delete,

        /// <summary>
        /// Add operation
        /// </summary>
        Add,

        /// <summary>
        /// Update operation
        /// </summary>
        Update,

        /// <summary>
        /// Remove operation
        /// </summary>
        Remove,

        /// <summary>
        /// Bulk add operation
        /// </summary>
        BulkAdd,

        /// <summary>
        /// Transfer validation operation
        /// </summary>
        TransferValidation,

        /// <summary>
        /// Export operation (CSV/JSON compliance data export)
        /// </summary>
        Export
    }
}

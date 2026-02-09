namespace BiatecTokensApi.Models.ComplianceProfile
{
    /// <summary>
    /// Audit log entry for compliance profile changes
    /// </summary>
    public class ComplianceProfileAuditEntry
    {
        /// <summary>
        /// Unique identifier for the audit entry
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Compliance profile ID that was changed
        /// </summary>
        public string ProfileId { get; set; } = string.Empty;

        /// <summary>
        /// User ID who owns the profile
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// Action performed (Created, Updated, Deleted)
        /// </summary>
        public string Action { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp when the action occurred
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// User ID who performed the action
        /// </summary>
        public string PerformedBy { get; set; } = string.Empty;

        /// <summary>
        /// Fields that were changed (field name -> new value)
        /// </summary>
        public Dictionary<string, string> ChangedFields { get; set; } = new();

        /// <summary>
        /// Previous values of changed fields (for updates)
        /// </summary>
        public Dictionary<string, string> PreviousValues { get; set; } = new();

        /// <summary>
        /// IP address of the request (if available)
        /// </summary>
        public string? IpAddress { get; set; }

        /// <summary>
        /// User agent of the request (if available)
        /// </summary>
        public string? UserAgent { get; set; }
    }
}

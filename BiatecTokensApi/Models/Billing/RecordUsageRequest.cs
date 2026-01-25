using System.ComponentModel.DataAnnotations;

namespace BiatecTokensApi.Models.Billing
{
    /// <summary>
    /// Request model for recording usage events
    /// </summary>
    /// <remarks>
    /// This model is used to manually record usage events for billing purposes.
    /// It allows external systems or manual processes to track operations that
    /// are not automatically captured by the platform.
    /// </remarks>
    public class RecordUsageRequest
    {
        /// <summary>
        /// Type of operation to record
        /// </summary>
        [Required]
        public OperationType OperationType { get; set; }

        /// <summary>
        /// Number of operations to record (default: 1, minimum: 1)
        /// </summary>
        [Range(1, int.MaxValue, ErrorMessage = "Operation count must be at least 1")]
        public int OperationCount { get; set; } = 1;

        /// <summary>
        /// Optional asset ID for context
        /// </summary>
        public ulong? AssetId { get; set; }

        /// <summary>
        /// Optional network for context
        /// </summary>
        public string? Network { get; set; }

        /// <summary>
        /// Optional notes about the operation
        /// </summary>
        public string? Notes { get; set; }
    }

    /// <summary>
    /// Response model for record usage endpoint
    /// </summary>
    public class RecordUsageResponse
    {
        /// <summary>
        /// Whether the request was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Number of operations recorded
        /// </summary>
        public int RecordedCount { get; set; }

        /// <summary>
        /// Current usage after recording
        /// </summary>
        public int CurrentUsage { get; set; }

        /// <summary>
        /// Maximum allowed for this operation type (-1 for unlimited)
        /// </summary>
        public int MaxAllowed { get; set; }

        /// <summary>
        /// Remaining capacity after recording (-1 for unlimited)
        /// </summary>
        public int RemainingCapacity { get; set; }

        /// <summary>
        /// Warning message if approaching limits
        /// </summary>
        public string? WarningMessage { get; set; }

        /// <summary>
        /// Error message if the request failed
        /// </summary>
        public string? ErrorMessage { get; set; }
    }
}

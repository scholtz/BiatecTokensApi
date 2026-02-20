namespace BiatecTokensApi.Models.TokenOperationsIntelligence
{
    /// <summary>
    /// Normalized token-affecting event with canonical structure
    /// </summary>
    public class NormalizedTokenEvent
    {
        /// <summary>
        /// Unique event identifier
        /// </summary>
        public string EventId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Canonical UTC timestamp of the event
        /// </summary>
        public DateTime OccurredAt { get; set; }

        /// <summary>
        /// Event category
        /// </summary>
        public TokenEventCategory Category { get; set; }

        /// <summary>
        /// Impact level of this event
        /// </summary>
        public EventImpact Impact { get; set; }

        /// <summary>
        /// Actor who initiated the event (address or system identifier)
        /// </summary>
        public string Actor { get; set; } = string.Empty;

        /// <summary>
        /// Short description of the event
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Blockchain transaction ID (if applicable)
        /// </summary>
        public string? TransactionId { get; set; }

        /// <summary>
        /// Block/round number where the event occurred (if applicable)
        /// </summary>
        public ulong? BlockRound { get; set; }

        /// <summary>
        /// Additional event-specific details (included when IncludeEventDetails = true)
        /// </summary>
        public Dictionary<string, object>? Details { get; set; }
    }
}

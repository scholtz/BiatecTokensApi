namespace BiatecTokensApi.Configuration
{
    /// <summary>
    /// Configuration for compliance capability matrix
    /// </summary>
    public class CapabilityMatrixConfig
    {
        /// <summary>
        /// Path to the capability matrix configuration file
        /// </summary>
        public string ConfigFilePath { get; set; } = "compliance-capabilities.json";

        /// <summary>
        /// Version of the capability matrix
        /// </summary>
        public string Version { get; set; } = "2026-02-05";

        /// <summary>
        /// Enable strict validation mode (deny by default if rule not found)
        /// </summary>
        public bool StrictMode { get; set; } = true;

        /// <summary>
        /// Enable caching of capability matrix
        /// </summary>
        public bool EnableCaching { get; set; } = true;

        /// <summary>
        /// Cache duration in seconds
        /// </summary>
        public int CacheDurationSeconds { get; set; } = 3600;
    }
}

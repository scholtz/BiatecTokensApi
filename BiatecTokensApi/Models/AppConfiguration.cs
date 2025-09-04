namespace BiatecTokensApi.Models
{
    /// <summary>
    /// Represents the configuration settings for an application, including account-related information.
    /// </summary>
    /// <remarks>This class is typically used to store and manage application-level configuration
    /// values.</remarks>
    public class AppConfiguration
    {
        /// <summary>
        /// Deployer's account
        /// </summary>
        public string Account { get; set; } = string.Empty;
    }
}

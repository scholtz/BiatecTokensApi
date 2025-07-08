namespace BiatecTokensApi.Models
{
    /// <summary>
    /// Base response model for token deployment operations
    /// </summary>
    public class BaseResponse
    {
        /// <summary>
        /// Error message if deployment failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Status of the deployment
        /// </summary>
        public bool Success { get; set; }
    }
}

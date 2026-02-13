namespace BiatecTokensApi.Models.Kyc
{
    /// <summary>
    /// Supported KYC providers
    /// </summary>
    public enum KycProvider
    {
        /// <summary>
        /// Mock provider for testing and development
        /// </summary>
        Mock = 0,

        /// <summary>
        /// Real provider integration (future implementation)
        /// </summary>
        External = 1
    }
}

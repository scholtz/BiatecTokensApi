namespace BiatecTokensApi.Models.Kyc
{
    /// <summary>
    /// Represents the status of a KYC verification
    /// </summary>
    public enum KycStatus
    {
        /// <summary>
        /// KYC verification has not been started
        /// </summary>
        NotStarted = 0,

        /// <summary>
        /// KYC verification is pending (awaiting provider response)
        /// </summary>
        Pending = 1,

        /// <summary>
        /// KYC verification is under manual review
        /// </summary>
        NeedsReview = 2,

        /// <summary>
        /// KYC verification was approved
        /// </summary>
        Approved = 3,

        /// <summary>
        /// KYC verification was rejected
        /// </summary>
        Rejected = 4,

        /// <summary>
        /// KYC verification has expired and needs to be renewed
        /// </summary>
        Expired = 5
    }
}

namespace BiatecTokensApi.Models.ComplianceProfile
{
    /// <summary>
    /// Response for compliance profile operations
    /// </summary>
    public class ComplianceProfileResponse : BaseResponse
    {
        /// <summary>
        /// The compliance profile data
        /// </summary>
        public ComplianceProfile? Profile { get; set; }

        /// <summary>
        /// Indicates if onboarding is complete
        /// </summary>
        public bool IsOnboardingComplete { get; set; }
    }

    /// <summary>
    /// Response for compliance profile validation errors
    /// </summary>
    public class ComplianceProfileValidationError
    {
        /// <summary>
        /// Field name that failed validation
        /// </summary>
        public string Field { get; set; } = string.Empty;

        /// <summary>
        /// Error message
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Error code
        /// </summary>
        public string Code { get; set; } = string.Empty;
    }
}

using System.ComponentModel.DataAnnotations;

namespace BiatecTokensApi.Models.Auth
{
    /// <summary>
    /// Request model for changing user password
    /// </summary>
    public class ChangePasswordRequest
    {
        /// <summary>
        /// Current password for verification
        /// </summary>
        [Required(ErrorMessage = "Current password is required")]
        public string CurrentPassword { get; set; } = string.Empty;

        /// <summary>
        /// New password to set
        /// </summary>
        [Required(ErrorMessage = "New password is required")]
        [MinLength(8, ErrorMessage = "New password must be at least 8 characters")]
        public string NewPassword { get; set; } = string.Empty;
    }
}

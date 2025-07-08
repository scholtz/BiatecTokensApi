using System.ComponentModel.DataAnnotations;

namespace BiatecTokensApi.Models.ARC3
{
    /// <summary>
    /// Localization information for ARC3 token metadata
    /// </summary>
    public class ARC3TokenLocalization
    {
        /// <summary>
        /// The URI pattern to fetch localized data from. This URI should contain the substring `{locale}`
        /// </summary>
        [Required]
        public required string Uri { get; set; }

        /// <summary>
        /// The locale of the default data within the base JSON
        /// </summary>
        [Required]
        public required string Default { get; set; }

        /// <summary>
        /// The list of locales for which data is available
        /// </summary>
        [Required]
        public required List<string> Locales { get; set; }

        /// <summary>
        /// The SHA-256 digests of the localized JSON files (except the default one)
        /// </summary>
        public Dictionary<string, string>? Integrity { get; set; }
    }
}

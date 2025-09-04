using System.ComponentModel.DataAnnotations;

namespace BiatecTokensApi.Models.ARC3
{
    /// <summary>
    /// ARC3 compliant token metadata structure
    /// </summary>
    public class ARC3TokenMetadata
    {
        /// <summary>
        /// Identifies the asset to which this token represents
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// The number of decimal places that the token amount should display
        /// </summary>
        public int? Decimals { get; set; }

        /// <summary>
        /// Describes the asset to which this token represents
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// A URI pointing to a file with MIME type image/* representing the asset
        /// </summary>
        public string? Image { get; set; }

        /// <summary>
        /// The SHA-256 digest of the file pointed by the URI image
        /// </summary>
        public string? ImageIntegrity { get; set; }

        /// <summary>
        /// The MIME type of the file pointed by the URI image. MUST be of the form 'image/*'
        /// </summary>
        public string? ImageMimetype { get; set; }

        /// <summary>
        /// Background color to display the asset. MUST be a six-character hexadecimal without a pre-pended #
        /// </summary>
        [RegularExpression(@"^[0-9A-Fa-f]{6}$", ErrorMessage = "Background color must be a six-character hexadecimal without #")]
        public string? BackgroundColor { get; set; }

        /// <summary>
        /// A URI pointing to an external website presenting the asset
        /// </summary>
        public string? ExternalUrl { get; set; }

        /// <summary>
        /// The SHA-256 digest of the file pointed by the URI external_url
        /// </summary>
        public string? ExternalUrlIntegrity { get; set; }

        /// <summary>
        /// The MIME type of the file pointed by the URI external_url
        /// </summary>
        public string? ExternalUrlMimetype { get; set; }

        /// <summary>
        /// A URI pointing to a multi-media file representing the asset
        /// </summary>
        public string? AnimationUrl { get; set; }

        /// <summary>
        /// The SHA-256 digest of the file pointed by the URI animation_url
        /// </summary>
        public string? AnimationUrlIntegrity { get; set; }

        /// <summary>
        /// The MIME type of the file pointed by the URI animation_url
        /// </summary>
        public string? AnimationUrlMimetype { get; set; }

        /// <summary>
        /// Arbitrary properties (also called attributes). Values may be strings, numbers, object or arrays
        /// </summary>
        public Dictionary<string, object>? Properties { get; set; }

        /// <summary>
        /// Extra metadata in base64
        /// </summary>
        public string? ExtraMetadata { get; set; }

        /// <summary>
        /// Localization information for the metadata
        /// </summary>
        public ARC3TokenLocalization? Localization { get; set; }
    }
}

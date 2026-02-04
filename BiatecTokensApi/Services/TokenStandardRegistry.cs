using BiatecTokensApi.Models.TokenStandards;
using BiatecTokensApi.Services.Interface;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Registry service for managing token standard profiles
    /// </summary>
    public class TokenStandardRegistry : ITokenStandardRegistry
    {
        private readonly ILogger<TokenStandardRegistry> _logger;
        private readonly List<TokenStandardProfile> _profiles;

        /// <summary>
        /// Initializes a new instance of the TokenStandardRegistry
        /// </summary>
        /// <param name="logger">Logger instance</param>
        public TokenStandardRegistry(ILogger<TokenStandardRegistry> logger)
        {
            _logger = logger;
            _profiles = InitializeStandardProfiles();
        }

        /// <summary>
        /// Gets all available token standard profiles
        /// </summary>
        public Task<List<TokenStandardProfile>> GetAllStandardsAsync(bool activeOnly = true)
        {
            var standards = activeOnly 
                ? _profiles.Where(p => p.IsActive).ToList() 
                : _profiles.ToList();
            
            return Task.FromResult(standards);
        }

        /// <summary>
        /// Gets a specific token standard profile by standard type
        /// </summary>
        public Task<TokenStandardProfile?> GetStandardProfileAsync(TokenStandard standard)
        {
            var profile = _profiles.FirstOrDefault(p => p.Standard == standard && p.IsActive);
            return Task.FromResult(profile);
        }

        /// <summary>
        /// Gets the default token standard for backward compatibility
        /// </summary>
        public Task<TokenStandardProfile> GetDefaultStandardAsync()
        {
            var defaultProfile = _profiles.First(p => p.Standard == TokenStandard.Baseline);
            return Task.FromResult(defaultProfile);
        }

        /// <summary>
        /// Checks if a token standard is supported
        /// </summary>
        public Task<bool> IsStandardSupportedAsync(TokenStandard standard)
        {
            var isSupported = _profiles.Any(p => p.Standard == standard && p.IsActive);
            return Task.FromResult(isSupported);
        }

        /// <summary>
        /// Initializes all supported token standard profiles
        /// </summary>
        private List<TokenStandardProfile> InitializeStandardProfiles()
        {
            return new List<TokenStandardProfile>
            {
                CreateBaselineProfile(),
                CreateARC3Profile(),
                CreateARC19Profile(),
                CreateARC69Profile(),
                CreateERC20Profile()
            };
        }

        /// <summary>
        /// Creates the Baseline standard profile
        /// </summary>
        private TokenStandardProfile CreateBaselineProfile()
        {
            return new TokenStandardProfile
            {
                Id = "baseline-1.0",
                Name = "Baseline",
                Version = "1.0.0",
                Description = "Minimal validation requirements for backward compatibility",
                Standard = TokenStandard.Baseline,
                RequiredFields = new List<StandardFieldDefinition>
                {
                    new StandardFieldDefinition
                    {
                        Name = "name",
                        DataType = "string",
                        Description = "Token name",
                        IsRequired = true,
                        MaxLength = 256
                    }
                },
                OptionalFields = new List<StandardFieldDefinition>
                {
                    new StandardFieldDefinition
                    {
                        Name = "decimals",
                        DataType = "number",
                        Description = "Number of decimal places",
                        IsRequired = false,
                        MinValue = 0,
                        MaxValue = 18
                    },
                    new StandardFieldDefinition
                    {
                        Name = "description",
                        DataType = "string",
                        Description = "Token description",
                        IsRequired = false,
                        MaxLength = 1000
                    }
                },
                ValidationRules = new List<ValidationRule>(),
                IsActive = true,
                SpecificationUrl = null
            };
        }

        /// <summary>
        /// Creates the ARC-3 standard profile
        /// </summary>
        private TokenStandardProfile CreateARC3Profile()
        {
            return new TokenStandardProfile
            {
                Id = "arc3-1.0",
                Name = "ARC-3",
                Version = "1.0.0",
                Description = "Algorand Request for Comments 3 - Rich metadata standard for NFTs and tokens",
                Standard = TokenStandard.ARC3,
                RequiredFields = new List<StandardFieldDefinition>
                {
                    new StandardFieldDefinition
                    {
                        Name = "name",
                        DataType = "string",
                        Description = "Identifies the asset to which this token represents",
                        IsRequired = true,
                        MaxLength = 256
                    }
                },
                OptionalFields = new List<StandardFieldDefinition>
                {
                    new StandardFieldDefinition
                    {
                        Name = "decimals",
                        DataType = "number",
                        Description = "The number of decimal places that the token amount should display",
                        IsRequired = false,
                        MinValue = 0,
                        MaxValue = 19
                    },
                    new StandardFieldDefinition
                    {
                        Name = "description",
                        DataType = "string",
                        Description = "Describes the asset to which this token represents",
                        IsRequired = false,
                        MaxLength = 1000
                    },
                    new StandardFieldDefinition
                    {
                        Name = "image",
                        DataType = "string",
                        Description = "A URI pointing to a file with MIME type image/*",
                        IsRequired = false
                    },
                    new StandardFieldDefinition
                    {
                        Name = "image_integrity",
                        DataType = "string",
                        Description = "The SHA-256 digest of the file pointed by the URI image",
                        IsRequired = false
                    },
                    new StandardFieldDefinition
                    {
                        Name = "image_mimetype",
                        DataType = "string",
                        Description = "The MIME type of the file pointed by the URI image",
                        IsRequired = false,
                        ValidationPattern = "^image/.*"
                    },
                    new StandardFieldDefinition
                    {
                        Name = "background_color",
                        DataType = "string",
                        Description = "Background color (six-character hexadecimal without #)",
                        IsRequired = false,
                        ValidationPattern = "^[0-9A-Fa-f]{6}$"
                    },
                    new StandardFieldDefinition
                    {
                        Name = "external_url",
                        DataType = "string",
                        Description = "A URI pointing to an external website presenting the asset",
                        IsRequired = false
                    },
                    new StandardFieldDefinition
                    {
                        Name = "animation_url",
                        DataType = "string",
                        Description = "A URI pointing to a multi-media file representing the asset",
                        IsRequired = false
                    },
                    new StandardFieldDefinition
                    {
                        Name = "properties",
                        DataType = "object",
                        Description = "Arbitrary properties (attributes)",
                        IsRequired = false
                    }
                },
                ValidationRules = new List<ValidationRule>
                {
                    new ValidationRule
                    {
                        Id = "arc3-image-mimetype",
                        Name = "Image MIME type validation",
                        Description = "If image is provided, image_mimetype should start with 'image/'",
                        ErrorMessage = "Image MIME type must start with 'image/'",
                        ErrorCode = "ARC3_INVALID_IMAGE_MIMETYPE",
                        Severity = TokenValidationSeverity.Warning
                    },
                    new ValidationRule
                    {
                        Id = "arc3-background-color",
                        Name = "Background color format",
                        Description = "Background color must be a six-character hexadecimal without #",
                        ErrorMessage = "Background color must be in format RRGGBB (e.g., 'FF0000' for red)",
                        ErrorCode = "ARC3_INVALID_BACKGROUND_COLOR",
                        Severity = TokenValidationSeverity.Error
                    }
                },
                IsActive = true,
                SpecificationUrl = "https://github.com/algorandfoundation/ARCs/blob/main/ARCs/arc-0003.md",
                ExampleJson = @"{
  ""name"": ""My Token"",
  ""decimals"": 6,
  ""description"": ""A sample ARC-3 token"",
  ""image"": ""ipfs://QmXyz..."",
  ""image_integrity"": ""sha256-abc123..."",
  ""image_mimetype"": ""image/png"",
  ""properties"": {
    ""category"": ""utility"",
    ""supply"": 1000000
  }
}"
            };
        }

        /// <summary>
        /// Creates the ARC-19 standard profile
        /// </summary>
        private TokenStandardProfile CreateARC19Profile()
        {
            return new TokenStandardProfile
            {
                Id = "arc19-1.0",
                Name = "ARC-19",
                Version = "1.0.0",
                Description = "Algorand Request for Comments 19 - On-chain metadata standard",
                Standard = TokenStandard.ARC19,
                RequiredFields = new List<StandardFieldDefinition>
                {
                    new StandardFieldDefinition
                    {
                        Name = "name",
                        DataType = "string",
                        Description = "Asset name stored on-chain",
                        IsRequired = true,
                        MaxLength = 32
                    },
                    new StandardFieldDefinition
                    {
                        Name = "unit_name",
                        DataType = "string",
                        Description = "Asset unit name",
                        IsRequired = true,
                        MaxLength = 8
                    }
                },
                OptionalFields = new List<StandardFieldDefinition>
                {
                    new StandardFieldDefinition
                    {
                        Name = "url",
                        DataType = "string",
                        Description = "Asset URL pointing to metadata",
                        IsRequired = false,
                        MaxLength = 96
                    },
                    new StandardFieldDefinition
                    {
                        Name = "decimals",
                        DataType = "number",
                        Description = "Number of decimals",
                        IsRequired = false,
                        MinValue = 0,
                        MaxValue = 19
                    }
                },
                ValidationRules = new List<ValidationRule>
                {
                    new ValidationRule
                    {
                        Id = "arc19-name-length",
                        Name = "Name length constraint",
                        Description = "Asset name must not exceed 32 characters for on-chain storage",
                        ErrorMessage = "Asset name must be 32 characters or less",
                        ErrorCode = "ARC19_NAME_TOO_LONG",
                        Severity = TokenValidationSeverity.Error
                    },
                    new ValidationRule
                    {
                        Id = "arc19-unit-name-length",
                        Name = "Unit name length constraint",
                        Description = "Unit name must not exceed 8 characters",
                        ErrorMessage = "Unit name must be 8 characters or less",
                        ErrorCode = "ARC19_UNIT_NAME_TOO_LONG",
                        Severity = TokenValidationSeverity.Error
                    }
                },
                IsActive = true,
                SpecificationUrl = "https://github.com/algorandfoundation/ARCs/blob/main/ARCs/arc-0019.md"
            };
        }

        /// <summary>
        /// Creates the ARC-69 standard profile
        /// </summary>
        private TokenStandardProfile CreateARC69Profile()
        {
            return new TokenStandardProfile
            {
                Id = "arc69-1.0",
                Name = "ARC-69",
                Version = "1.0.0",
                Description = "Algorand Request for Comments 69 - Simplified metadata standard",
                Standard = TokenStandard.ARC69,
                RequiredFields = new List<StandardFieldDefinition>
                {
                    new StandardFieldDefinition
                    {
                        Name = "standard",
                        DataType = "string",
                        Description = "Must be 'arc69'",
                        IsRequired = true
                    }
                },
                OptionalFields = new List<StandardFieldDefinition>
                {
                    new StandardFieldDefinition
                    {
                        Name = "description",
                        DataType = "string",
                        Description = "Description of the asset",
                        IsRequired = false
                    },
                    new StandardFieldDefinition
                    {
                        Name = "external_url",
                        DataType = "string",
                        Description = "URL to external website",
                        IsRequired = false
                    },
                    new StandardFieldDefinition
                    {
                        Name = "media_url",
                        DataType = "string",
                        Description = "URL to media file",
                        IsRequired = false
                    },
                    new StandardFieldDefinition
                    {
                        Name = "properties",
                        DataType = "object",
                        Description = "Arbitrary properties",
                        IsRequired = false
                    },
                    new StandardFieldDefinition
                    {
                        Name = "mime_type",
                        DataType = "string",
                        Description = "MIME type of the media",
                        IsRequired = false
                    }
                },
                ValidationRules = new List<ValidationRule>
                {
                    new ValidationRule
                    {
                        Id = "arc69-standard-field",
                        Name = "Standard field value",
                        Description = "The 'standard' field must be set to 'arc69'",
                        ErrorMessage = "The 'standard' field must equal 'arc69'",
                        ErrorCode = "ARC69_INVALID_STANDARD_FIELD",
                        Severity = TokenValidationSeverity.Error
                    }
                },
                IsActive = true,
                SpecificationUrl = "https://github.com/algorandfoundation/ARCs/blob/main/ARCs/arc-0069.md"
            };
        }

        /// <summary>
        /// Creates the ERC-20 standard profile
        /// </summary>
        private TokenStandardProfile CreateERC20Profile()
        {
            return new TokenStandardProfile
            {
                Id = "erc20-1.0",
                Name = "ERC-20",
                Version = "1.0.0",
                Description = "Ethereum Request for Comments 20 - Fungible token standard",
                Standard = TokenStandard.ERC20,
                RequiredFields = new List<StandardFieldDefinition>
                {
                    new StandardFieldDefinition
                    {
                        Name = "name",
                        DataType = "string",
                        Description = "Token name",
                        IsRequired = true,
                        MaxLength = 256
                    },
                    new StandardFieldDefinition
                    {
                        Name = "symbol",
                        DataType = "string",
                        Description = "Token symbol",
                        IsRequired = true,
                        MaxLength = 11
                    },
                    new StandardFieldDefinition
                    {
                        Name = "decimals",
                        DataType = "number",
                        Description = "Number of decimal places",
                        IsRequired = true,
                        MinValue = 0,
                        MaxValue = 18
                    }
                },
                OptionalFields = new List<StandardFieldDefinition>
                {
                    new StandardFieldDefinition
                    {
                        Name = "totalSupply",
                        DataType = "string",
                        Description = "Total token supply",
                        IsRequired = false
                    }
                },
                ValidationRules = new List<ValidationRule>
                {
                    new ValidationRule
                    {
                        Id = "erc20-symbol-length",
                        Name = "Symbol length constraint",
                        Description = "Token symbol should be 11 characters or less",
                        ErrorMessage = "Token symbol must be 11 characters or less",
                        ErrorCode = "ERC20_SYMBOL_TOO_LONG",
                        Severity = TokenValidationSeverity.Error
                    },
                    new ValidationRule
                    {
                        Id = "erc20-decimals-range",
                        Name = "Decimals range validation",
                        Description = "Decimals must be between 0 and 18",
                        ErrorMessage = "Decimals must be between 0 and 18",
                        ErrorCode = "ERC20_INVALID_DECIMALS",
                        Severity = TokenValidationSeverity.Error
                    }
                },
                IsActive = true,
                SpecificationUrl = "https://eips.ethereum.org/EIPS/eip-20"
            };
        }
    }
}

using BiatecTokensApi.Models;
using BiatecTokensApi.Models.TokenStandards;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Unit tests for TokenStandardValidator service
    /// </summary>
    [TestFixture]
    public class TokenStandardValidatorTests
    {
        private Mock<ILogger<TokenStandardValidator>> _loggerMock;
        private Mock<ITokenStandardRegistry> _registryMock;
        private TokenStandardValidator _validator;

        [SetUp]
        public void SetUp()
        {
            _loggerMock = new Mock<ILogger<TokenStandardValidator>>();
            _registryMock = new Mock<ITokenStandardRegistry>();
            _validator = new TokenStandardValidator(_loggerMock.Object, _registryMock.Object);
        }

        [Test]
        public async Task ValidateAsync_ReturnsError_WhenStandardNotSupported()
        {
            // Arrange
            _registryMock.Setup(r => r.GetStandardProfileAsync(It.IsAny<TokenStandard>()))
                .ReturnsAsync((TokenStandardProfile?)null);

            // Act
            var result = await _validator.ValidateAsync(TokenStandard.ARC3, new { name = "Test" });

            // Assert
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors.Any(e => e.Code == ErrorCodes.INVALID_TOKEN_STANDARD), Is.True);
        }

        [Test]
        public async Task ValidateAsync_PassesValidation_ForValidBaselineMetadata()
        {
            // Arrange
            var profile = CreateBaselineProfile();
            _registryMock.Setup(r => r.GetStandardProfileAsync(TokenStandard.Baseline))
                .ReturnsAsync(profile);

            var metadata = new Dictionary<string, object>
            {
                { "name", "My Token" }
            };

            // Act
            var result = await _validator.ValidateAsync(TokenStandard.Baseline, metadata);

            // Assert
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Errors, Is.Empty);
        }

        [Test]
        public async Task ValidateAsync_FailsValidation_WhenRequiredFieldMissing()
        {
            // Arrange
            var profile = CreateBaselineProfile();
            _registryMock.Setup(r => r.GetStandardProfileAsync(TokenStandard.Baseline))
                .ReturnsAsync(profile);

            var metadata = new Dictionary<string, object>();

            // Act
            var result = await _validator.ValidateAsync(TokenStandard.Baseline, metadata);

            // Assert
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors.Any(e => 
                e.Code == ErrorCodes.REQUIRED_METADATA_FIELD_MISSING && e.Field == "name"), Is.True);
        }

        [Test]
        public async Task ValidateAsync_ValidatesStringMaxLength()
        {
            // Arrange
            var profile = CreateBaselineProfile();
            _registryMock.Setup(r => r.GetStandardProfileAsync(TokenStandard.Baseline))
                .ReturnsAsync(profile);

            var longName = new string('A', 300); // Exceeds max length of 256
            var metadata = new Dictionary<string, object>
            {
                { "name", longName }
            };

            // Act
            var result = await _validator.ValidateAsync(TokenStandard.Baseline, metadata);

            // Assert
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors.Any(e => 
                e.Code == ErrorCodes.METADATA_FIELD_VALIDATION_FAILED && e.Field == "name"), Is.True);
        }

        [Test]
        public async Task ValidateAsync_ValidatesNumericRange()
        {
            // Arrange
            var profile = CreateERC20Profile();
            _registryMock.Setup(r => r.GetStandardProfileAsync(TokenStandard.ERC20))
                .ReturnsAsync(profile);

            var metadata = new Dictionary<string, object>
            {
                { "name", "Token" },
                { "symbol", "TKN" },
                { "decimals", 25 } // Exceeds max of 18
            };

            // Act
            var result = await _validator.ValidateAsync(TokenStandard.ERC20, metadata);

            // Assert
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors.Any(e => 
                e.Code == ErrorCodes.METADATA_FIELD_VALIDATION_FAILED && e.Field == "decimals"), Is.True);
        }

        [Test]
        public async Task ValidateAsync_ValidatesRegexPattern()
        {
            // Arrange
            var profile = CreateARC3Profile();
            _registryMock.Setup(r => r.GetStandardProfileAsync(TokenStandard.ARC3))
                .ReturnsAsync(profile);

            var metadata = new Dictionary<string, object>
            {
                { "name", "Test Token" },
                { "background_color", "GGGGGG" } // Invalid hex color
            };

            // Act
            var result = await _validator.ValidateAsync(TokenStandard.ARC3, metadata);

            // Assert
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors.Any(e => 
                e.Code == ErrorCodes.METADATA_FIELD_VALIDATION_FAILED && e.Field == "background_color"), Is.True);
        }

        [Test]
        public async Task ValidateAsync_AcceptsValidRegexPattern()
        {
            // Arrange
            var profile = CreateARC3Profile();
            _registryMock.Setup(r => r.GetStandardProfileAsync(TokenStandard.ARC3))
                .ReturnsAsync(profile);

            var metadata = new Dictionary<string, object>
            {
                { "name", "Test Token" },
                { "background_color", "FF0000" } // Valid hex color
            };

            // Act
            var result = await _validator.ValidateAsync(TokenStandard.ARC3, metadata);

            // Assert
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Errors, Is.Empty);
        }

        [Test]
        public async Task ValidateAsync_IncludesContextFields()
        {
            // Arrange
            var profile = CreateERC20Profile();
            _registryMock.Setup(r => r.GetStandardProfileAsync(TokenStandard.ERC20))
                .ReturnsAsync(profile);

            // Act - Not including required fields in metadata, but passing them as context
            var result = await _validator.ValidateAsync(
                TokenStandard.ERC20, 
                null, 
                tokenName: "My Token",
                tokenSymbol: "MTK",
                decimals: 6);

            // Assert
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Errors, Is.Empty);
        }

        [Test]
        public async Task ValidateAsync_ValidatesTypeCompatibility()
        {
            // Arrange
            var profile = CreateERC20Profile();
            _registryMock.Setup(r => r.GetStandardProfileAsync(TokenStandard.ERC20))
                .ReturnsAsync(profile);

            var metadata = new Dictionary<string, object>
            {
                { "name", "Token" },
                { "symbol", "TKN" },
                { "decimals", "not a number" } // Wrong type
            };

            // Act
            var result = await _validator.ValidateAsync(TokenStandard.ERC20, metadata);

            // Assert
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors.Any(e => 
                e.Code == ErrorCodes.METADATA_FIELD_TYPE_MISMATCH && e.Field == "decimals"), Is.True);
        }

        [Test]
        public async Task ValidateAsync_AppliesARC3CustomRules()
        {
            // Arrange
            var profile = CreateARC3Profile();
            _registryMock.Setup(r => r.GetStandardProfileAsync(TokenStandard.ARC3))
                .ReturnsAsync(profile);

            var metadata = new Dictionary<string, object>
            {
                { "name", "Test" },
                { "image", "ipfs://test" },
                { "image_mimetype", "video/mp4" } // Should be image/*
            };

            // Act
            var result = await _validator.ValidateAsync(TokenStandard.ARC3, metadata);

            // Assert
            Assert.That(result.Warnings.Any(w => w.Code == "ARC3_INVALID_IMAGE_MIMETYPE"), Is.True);
        }

        [Test]
        public async Task ValidateAsync_AppliesARC19CustomRules()
        {
            // Arrange
            var profile = CreateARC19Profile();
            _registryMock.Setup(r => r.GetStandardProfileAsync(TokenStandard.ARC19))
                .ReturnsAsync(profile);

            var metadata = new Dictionary<string, object>
            {
                { "name", "This is a very long token name that exceeds the limit" }, // > 32 chars
                { "unit_name", "TKN" }
            };

            // Act
            var result = await _validator.ValidateAsync(TokenStandard.ARC19, metadata);

            // Assert
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors.Any(e => e.Code == "ARC19_NAME_TOO_LONG"), Is.True);
        }

        [Test]
        public async Task ValidateAsync_AppliesARC69CustomRules()
        {
            // Arrange
            var profile = CreateARC69Profile();
            _registryMock.Setup(r => r.GetStandardProfileAsync(TokenStandard.ARC69))
                .ReturnsAsync(profile);

            var metadata = new Dictionary<string, object>
            {
                { "standard", "arc70" } // Wrong value
            };

            // Act
            var result = await _validator.ValidateAsync(TokenStandard.ARC69, metadata);

            // Assert
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors.Any(e => e.Code == "ARC69_INVALID_STANDARD_FIELD"), Is.True);
        }

        [Test]
        public async Task ValidateAsync_AppliesERC20CustomRules()
        {
            // Arrange
            var profile = CreateERC20Profile();
            _registryMock.Setup(r => r.GetStandardProfileAsync(TokenStandard.ERC20))
                .ReturnsAsync(profile);

            var metadata = new Dictionary<string, object>
            {
                { "name", "Token" },
                { "symbol", "VERYLONGSYMBOL" }, // > 11 chars
                { "decimals", 10 }
            };

            // Act
            var result = await _validator.ValidateAsync(TokenStandard.ERC20, metadata);

            // Assert
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors.Any(e => e.Code == "ERC20_SYMBOL_TOO_LONG"), Is.True);
        }

        [Test]
        public async Task ValidateAsync_ReturnsWarnings_WhenValidWithWarnings()
        {
            // Arrange
            var profile = CreateARC3Profile();
            _registryMock.Setup(r => r.GetStandardProfileAsync(TokenStandard.ARC3))
                .ReturnsAsync(profile);

            var metadata = new Dictionary<string, object>
            {
                { "name", "Test" },
                { "image", "ipfs://test" },
                { "image_mimetype", "application/pdf" } // Warning
            };

            // Act
            var result = await _validator.ValidateAsync(TokenStandard.ARC3, metadata);

            // Assert
            Assert.That(result.IsValid, Is.True); // Still valid despite warnings
            Assert.That(result.Warnings, Is.Not.Empty);
        }

        [Test]
        public async Task SupportsStandard_ReturnsTrue()
        {
            // Act
            var result = _validator.SupportsStandard(TokenStandard.ARC3);

            // Assert
            Assert.That(result, Is.True);
        }

        // Helper methods to create test profiles
        private TokenStandardProfile CreateBaselineProfile()
        {
            return new TokenStandardProfile
            {
                Id = "baseline-1.0",
                Name = "Baseline",
                Version = "1.0.0",
                Standard = TokenStandard.Baseline,
                RequiredFields = new List<StandardFieldDefinition>
                {
                    new StandardFieldDefinition
                    {
                        Name = "name",
                        DataType = "string",
                        IsRequired = true,
                        MaxLength = 256
                    }
                },
                ValidationRules = new List<ValidationRule>()
            };
        }

        private TokenStandardProfile CreateARC3Profile()
        {
            return new TokenStandardProfile
            {
                Id = "arc3-1.0",
                Name = "ARC-3",
                Version = "1.0.0",
                Standard = TokenStandard.ARC3,
                RequiredFields = new List<StandardFieldDefinition>
                {
                    new StandardFieldDefinition { Name = "name", DataType = "string", IsRequired = true }
                },
                OptionalFields = new List<StandardFieldDefinition>
                {
                    new StandardFieldDefinition 
                    { 
                        Name = "background_color", 
                        DataType = "string", 
                        ValidationPattern = @"^[0-9A-Fa-f]{6}$" 
                    },
                    new StandardFieldDefinition { Name = "image", DataType = "string" },
                    new StandardFieldDefinition { Name = "image_mimetype", DataType = "string" }
                },
                ValidationRules = new List<ValidationRule>
                {
                    new ValidationRule
                    {
                        Id = "arc3-image-mimetype",
                        ErrorCode = "ARC3_INVALID_IMAGE_MIMETYPE",
                        Severity = ValidationSeverity.Warning
                    },
                    new ValidationRule
                    {
                        Id = "arc3-background-color",
                        ErrorCode = "ARC3_INVALID_BACKGROUND_COLOR",
                        Severity = ValidationSeverity.Error
                    }
                }
            };
        }

        private TokenStandardProfile CreateARC19Profile()
        {
            return new TokenStandardProfile
            {
                Id = "arc19-1.0",
                Name = "ARC-19",
                Version = "1.0.0",
                Standard = TokenStandard.ARC19,
                RequiredFields = new List<StandardFieldDefinition>
                {
                    new StandardFieldDefinition 
                    { 
                        Name = "name", 
                        DataType = "string", 
                        IsRequired = true,
                        MaxLength = 32 
                    },
                    new StandardFieldDefinition 
                    { 
                        Name = "unit_name", 
                        DataType = "string", 
                        IsRequired = true,
                        MaxLength = 8 
                    }
                },
                ValidationRules = new List<ValidationRule>
                {
                    new ValidationRule
                    {
                        Id = "arc19-name-length",
                        ErrorCode = "ARC19_NAME_TOO_LONG",
                        Severity = ValidationSeverity.Error
                    }
                }
            };
        }

        private TokenStandardProfile CreateARC69Profile()
        {
            return new TokenStandardProfile
            {
                Id = "arc69-1.0",
                Name = "ARC-69",
                Version = "1.0.0",
                Standard = TokenStandard.ARC69,
                RequiredFields = new List<StandardFieldDefinition>
                {
                    new StandardFieldDefinition { Name = "standard", DataType = "string", IsRequired = true }
                },
                ValidationRules = new List<ValidationRule>
                {
                    new ValidationRule
                    {
                        Id = "arc69-standard-field",
                        ErrorCode = "ARC69_INVALID_STANDARD_FIELD",
                        Severity = ValidationSeverity.Error
                    }
                }
            };
        }

        private TokenStandardProfile CreateERC20Profile()
        {
            return new TokenStandardProfile
            {
                Id = "erc20-1.0",
                Name = "ERC-20",
                Version = "1.0.0",
                Standard = TokenStandard.ERC20,
                RequiredFields = new List<StandardFieldDefinition>
                {
                    new StandardFieldDefinition 
                    { 
                        Name = "name", 
                        DataType = "string", 
                        IsRequired = true 
                    },
                    new StandardFieldDefinition 
                    { 
                        Name = "symbol", 
                        DataType = "string", 
                        IsRequired = true,
                        MaxLength = 11 
                    },
                    new StandardFieldDefinition 
                    { 
                        Name = "decimals", 
                        DataType = "number", 
                        IsRequired = true,
                        MinValue = 0,
                        MaxValue = 18 
                    }
                },
                ValidationRules = new List<ValidationRule>
                {
                    new ValidationRule
                    {
                        Id = "erc20-symbol-length",
                        ErrorCode = "ERC20_SYMBOL_TOO_LONG",
                        Severity = ValidationSeverity.Error
                    },
                    new ValidationRule
                    {
                        Id = "erc20-decimals-range",
                        ErrorCode = "ERC20_INVALID_DECIMALS",
                        Severity = ValidationSeverity.Error
                    }
                }
            };
        }
    }
}

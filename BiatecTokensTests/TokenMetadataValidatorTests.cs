using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Unit tests for TokenMetadataValidator service
    /// </summary>
    [TestFixture]
    public class TokenMetadataValidatorTests
    {
        private Mock<ILogger<TokenMetadataValidator>> _loggerMock = null!;
        private TokenMetadataValidator _validator = null!;

        [SetUp]
        public void Setup()
        {
            _loggerMock = new Mock<ILogger<TokenMetadataValidator>>();
            _validator = new TokenMetadataValidator(_loggerMock.Object);
        }

        #region ARC3 Metadata Validation Tests

        [Test]
        public void ValidateARC3Metadata_ValidMetadata_ShouldPass()
        {
            // Arrange
            var metadata = new Dictionary<string, object>
            {
                { "name", "Test Token" },
                { "symbol", "TEST" },
                { "decimals", 6 },
                { "description", "A test token" },
                { "image", "https://example.com/image.png" }
            };

            // Act
            var result = _validator.ValidateARC3Metadata(metadata);

            // Assert
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Errors, Is.Empty);
            Assert.That(result.Summary, Does.Contain("valid"));
        }

        [Test]
        public void ValidateARC3Metadata_MissingRequiredFields_ShouldFail()
        {
            // Arrange
            var metadata = new Dictionary<string, object>
            {
                { "symbol", "TEST" }
            };

            // Act
            var result = _validator.ValidateARC3Metadata(metadata);

            // Assert
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors, Is.Not.Empty);
            Assert.That(result.Errors.Count, Is.GreaterThanOrEqualTo(2)); // Missing name and decimals
            
            var nameError = result.Errors.FirstOrDefault(e => e.Field == "name");
            Assert.That(nameError, Is.Not.Null);
            Assert.That(nameError!.Message, Does.Contain("Required"));
        }

        [Test]
        public void ValidateARC3Metadata_InvalidDecimals_ShouldFail()
        {
            // Arrange
            var metadata = new Dictionary<string, object>
            {
                { "name", "Test Token" },
                { "decimals", 25 } // Invalid: exceeds maximum of 19
            };

            // Act
            var result = _validator.ValidateARC3Metadata(metadata);

            // Assert
            Assert.That(result.IsValid, Is.False);
            var decimalsError = result.Errors.FirstOrDefault(e => e.Field == "decimals");
            Assert.That(decimalsError, Is.Not.Null);
            Assert.That(decimalsError!.Message, Does.Contain("between 0 and 19"));
        }

        [Test]
        public void ValidateARC3Metadata_MissingOptionalFields_ShouldGenerateWarnings()
        {
            // Arrange
            var metadata = new Dictionary<string, object>
            {
                { "name", "Test Token" },
                { "decimals", 6 }
            };

            // Act
            var result = _validator.ValidateARC3Metadata(metadata);

            // Assert
            Assert.That(result.IsValid, Is.True); // Still valid
            Assert.That(result.Warnings, Is.Not.Empty);
            Assert.That(result.Warnings.Count, Is.GreaterThanOrEqualTo(1)); // Missing symbol, description, image
        }

        #endregion

        #region ARC200 Metadata Validation Tests

        [Test]
        public void ValidateARC200Metadata_ValidMetadata_ShouldPass()
        {
            // Arrange
            var metadata = new Dictionary<string, object>
            {
                { "name", "ARC200 Token" },
                { "symbol", "A200" },
                { "decimals", 18 }
            };

            // Act
            var result = _validator.ValidateARC200Metadata(metadata);

            // Assert
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Errors, Is.Empty);
        }

        [Test]
        public void ValidateARC200Metadata_InvalidDecimals_ShouldFail()
        {
            // Arrange
            var metadata = new Dictionary<string, object>
            {
                { "name", "ARC200 Token" },
                { "symbol", "A200" },
                { "decimals", 20 } // Invalid: exceeds ARC200 maximum of 18
            };

            // Act
            var result = _validator.ValidateARC200Metadata(metadata);

            // Assert
            Assert.That(result.IsValid, Is.False);
            var decimalsError = result.Errors.FirstOrDefault(e => e.Field == "decimals");
            Assert.That(decimalsError, Is.Not.Null);
            Assert.That(decimalsError!.Message, Does.Contain("between 0 and 18"));
        }

        [Test]
        public void ValidateARC200Metadata_MissingRequiredSymbol_ShouldFail()
        {
            // Arrange
            var metadata = new Dictionary<string, object>
            {
                { "name", "ARC200 Token" },
                { "decimals", 18 }
            };

            // Act
            var result = _validator.ValidateARC200Metadata(metadata);

            // Assert
            Assert.That(result.IsValid, Is.False);
            var symbolError = result.Errors.FirstOrDefault(e => e.Field == "symbol");
            Assert.That(symbolError, Is.Not.Null);
        }

        #endregion

        #region ERC20 Metadata Validation Tests

        [Test]
        public void ValidateERC20Metadata_ValidMetadata_ShouldPass()
        {
            // Arrange
            var metadata = new Dictionary<string, object>
            {
                { "name", "ERC20 Token" },
                { "symbol", "ERC" },
                { "decimals", 18 }
            };

            // Act
            var result = _validator.ValidateERC20Metadata(metadata);

            // Assert
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Errors, Is.Empty);
        }

        [Test]
        public void ValidateERC20Metadata_InvalidDecimals_ShouldFail()
        {
            // Arrange
            var metadata = new Dictionary<string, object>
            {
                { "name", "ERC20 Token" },
                { "symbol", "ERC" },
                { "decimals", 19 } // Invalid: exceeds ERC20 maximum of 18
            };

            // Act
            var result = _validator.ValidateERC20Metadata(metadata);

            // Assert
            Assert.That(result.IsValid, Is.False);
            var decimalsError = result.Errors.FirstOrDefault(e => e.Field == "decimals");
            Assert.That(decimalsError, Is.Not.Null);
        }

        #endregion

        #region ERC721 Metadata Validation Tests

        [Test]
        public void ValidateERC721Metadata_ValidMetadata_ShouldPass()
        {
            // Arrange
            var metadata = new Dictionary<string, object>
            {
                { "name", "NFT #1" },
                { "description", "A unique NFT" },
                { "image", "ipfs://QmHash" }
            };

            // Act
            var result = _validator.ValidateERC721Metadata(metadata);

            // Assert
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Errors, Is.Empty);
        }

        [Test]
        public void ValidateERC721Metadata_MissingName_ShouldFail()
        {
            // Arrange
            var metadata = new Dictionary<string, object>
            {
                { "description", "A unique NFT" }
            };

            // Act
            var result = _validator.ValidateERC721Metadata(metadata);

            // Assert
            Assert.That(result.IsValid, Is.False);
            var nameError = result.Errors.FirstOrDefault(e => e.Field == "name");
            Assert.That(nameError, Is.Not.Null);
        }

        #endregion

        #region Metadata Normalization Tests

        [Test]
        public void NormalizeMetadata_ARC3WithMissingFields_ShouldApplyDefaults()
        {
            // Arrange
            var metadata = new Dictionary<string, object>
            {
                { "decimals", 6 }
            };

            // Act
            var result = _validator.NormalizeMetadata(metadata, "ARC3");

            // Assert
            Assert.That(result.HasDefaults, Is.True);
            Assert.That(result.DefaultedFields, Is.Not.Empty);
            Assert.That(result.WarningSignals, Is.Not.Empty);
            
            // Verify defaults were applied
            var normalizedDict = result.Metadata as Dictionary<string, object>;
            Assert.That(normalizedDict, Is.Not.Null);
            Assert.That(normalizedDict!.ContainsKey("name"), Is.True);
            Assert.That(normalizedDict["name"].ToString(), Is.EqualTo("Unknown Token"));
        }

        [Test]
        public void NormalizeMetadata_ERC20WithCompleteData_ShouldNotAddDefaults()
        {
            // Arrange
            var metadata = new Dictionary<string, object>
            {
                { "name", "Complete Token" },
                { "symbol", "CPL" },
                { "decimals", 18 },
                { "description", "Fully specified" }
            };

            // Act
            var result = _validator.NormalizeMetadata(metadata, "ERC20");

            // Assert
            Assert.That(result.HasDefaults, Is.False);
            Assert.That(result.DefaultedFields, Is.Empty);
        }

        [Test]
        public void NormalizeMetadata_ERC721WithMissingImage_ShouldAddWarning()
        {
            // Arrange
            var metadata = new Dictionary<string, object>
            {
                { "name", "NFT without image" }
            };

            // Act
            var result = _validator.NormalizeMetadata(metadata, "ERC721");

            // Assert
            Assert.That(result.WarningSignals, Is.Not.Empty);
            var imageWarning = result.WarningSignals.FirstOrDefault(w => w.Contains("image"));
            Assert.That(imageWarning, Is.Not.Null);
        }

        #endregion

        #region Decimal Precision Validation Tests

        [Test]
        public void ValidateDecimalPrecision_ValidPrecision_ShouldPass()
        {
            // Arrange
            decimal amount = 123.456789m;
            int decimals = 6;

            // Act
            var result = _validator.ValidateDecimalPrecision(amount, decimals);

            // Assert
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.HasPrecisionLoss, Is.False);
        }

        [Test]
        public void ValidateDecimalPrecision_ExcessivePrecision_ShouldFail()
        {
            // Arrange
            decimal amount = 123.456789123456m;
            int decimals = 6;

            // Act
            var result = _validator.ValidateDecimalPrecision(amount, decimals);

            // Assert
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.HasPrecisionLoss, Is.True);
            Assert.That(result.ErrorMessage, Is.Not.Null);
            Assert.That(result.RecommendedValue, Is.Not.Null);
        }

        [Test]
        public void ValidateDecimalPrecision_WholeNumber_ShouldPass()
        {
            // Arrange
            decimal amount = 100m;
            int decimals = 6;

            // Act
            var result = _validator.ValidateDecimalPrecision(amount, decimals);

            // Assert
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.ActualPrecision, Is.EqualTo(0));
        }

        #endregion

        #region Balance Conversion Tests

        [Test]
        public void ConvertRawToDisplayBalance_ValidConversion_ShouldWork()
        {
            // Arrange
            string rawBalance = "1000000"; // 1 million micro-units
            int decimals = 6;

            // Act
            var result = _validator.ConvertRawToDisplayBalance(rawBalance, decimals);

            // Assert
            Assert.That(result, Is.EqualTo(1m));
        }

        [Test]
        public void ConvertRawToDisplayBalance_LargeNumber_ShouldWork()
        {
            // Arrange
            string rawBalance = "123456789123456789"; // Very large number
            int decimals = 18;

            // Act
            var result = _validator.ConvertRawToDisplayBalance(rawBalance, decimals);

            // Assert
            Assert.That(result, Is.GreaterThan(0));
            Assert.That(result, Is.LessThan(1m));
        }

        [Test]
        public void ConvertRawToDisplayBalance_ZeroDecimals_ShouldReturnExactValue()
        {
            // Arrange
            string rawBalance = "12345";
            int decimals = 0;

            // Act
            var result = _validator.ConvertRawToDisplayBalance(rawBalance, decimals);

            // Assert
            Assert.That(result, Is.EqualTo(12345m));
        }

        [Test]
        public void ConvertRawToDisplayBalance_InvalidFormat_ShouldReturnZero()
        {
            // Arrange
            string rawBalance = "not-a-number";
            int decimals = 6;

            // Act
            var result = _validator.ConvertRawToDisplayBalance(rawBalance, decimals);

            // Assert
            Assert.That(result, Is.EqualTo(0m));
        }

        [Test]
        public void ConvertDisplayToRawBalance_ValidConversion_ShouldWork()
        {
            // Arrange
            decimal displayBalance = 1m;
            int decimals = 6;

            // Act
            var result = _validator.ConvertDisplayToRawBalance(displayBalance, decimals);

            // Assert
            Assert.That(result, Is.EqualTo("1000000"));
        }

        [Test]
        public void ConvertDisplayToRawBalance_FractionalAmount_ShouldWork()
        {
            // Arrange
            decimal displayBalance = 0.123456m;
            int decimals = 6;

            // Act
            var result = _validator.ConvertDisplayToRawBalance(displayBalance, decimals);

            // Assert
            Assert.That(result, Is.EqualTo("123456"));
        }

        [Test]
        public void ConvertDisplayToRawBalance_ZeroDecimals_ShouldWork()
        {
            // Arrange
            decimal displayBalance = 12345m;
            int decimals = 0;

            // Act
            var result = _validator.ConvertDisplayToRawBalance(displayBalance, decimals);

            // Assert
            Assert.That(result, Is.EqualTo("12345"));
        }

        [Test]
        public void ConvertDisplayToRawBalance_RoundTripConversion_ShouldBeConsistent()
        {
            // Arrange
            string originalRaw = "1234567890";
            int decimals = 6;

            // Act - Convert to display and back
            var display = _validator.ConvertRawToDisplayBalance(originalRaw, decimals);
            var backToRaw = _validator.ConvertDisplayToRawBalance(display, decimals);

            // Assert
            Assert.That(backToRaw, Is.EqualTo(originalRaw));
        }

        #endregion

        #region Edge Case Tests

        [Test]
        public void ValidateARC3Metadata_NullMetadata_ShouldHandleGracefully()
        {
            // Arrange
            object? metadata = null;

            // Act & Assert - Should not throw
            var result = _validator.ValidateARC3Metadata(metadata!);
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors, Is.Not.Empty);
        }

        [Test]
        public void NormalizeMetadata_EmptyMetadata_ShouldApplyDefaults()
        {
            // Arrange
            var metadata = new Dictionary<string, object>();

            // Act
            var result = _validator.NormalizeMetadata(metadata, "ARC3");

            // Assert
            Assert.That(result.HasDefaults, Is.True);
            Assert.That(result.DefaultedFields, Is.Not.Empty);
        }

        [Test]
        public void ConvertRawToDisplayBalance_EmptyString_ShouldReturnZero()
        {
            // Arrange
            string rawBalance = "";
            int decimals = 6;

            // Act
            var result = _validator.ConvertRawToDisplayBalance(rawBalance, decimals);

            // Assert
            Assert.That(result, Is.EqualTo(0m));
        }

        [Test]
        public void ValidateDecimalPrecision_NegativeDecimals_ShouldHandleGracefully()
        {
            // Arrange
            decimal amount = 123.45m;
            int decimals = -1;

            // Act & Assert - Should not throw
            var result = _validator.ValidateDecimalPrecision(amount, decimals);
            // Behavior may vary, but should not crash
            Assert.That(result, Is.Not.Null);
        }

        #endregion

        #region Multi-Standard Validation Tests

        [Test]
        public void ValidateMetadata_DifferentStandards_ShouldApplyCorrectRules()
        {
            // Arrange
            var metadata = new Dictionary<string, object>
            {
                { "name", "Test" },
                { "symbol", "TST" },
                { "decimals", 19 } // Valid for ARC3, invalid for ARC200/ERC20
            };

            // Act
            var arc3Result = _validator.ValidateARC3Metadata(metadata);
            var arc200Result = _validator.ValidateARC200Metadata(metadata);
            var erc20Result = _validator.ValidateERC20Metadata(metadata);

            // Assert
            Assert.That(arc3Result.IsValid, Is.True, "ARC3 should allow 19 decimals");
            Assert.That(arc200Result.IsValid, Is.False, "ARC200 should reject 19 decimals");
            Assert.That(erc20Result.IsValid, Is.False, "ERC20 should reject 19 decimals");
        }

        #endregion
    }
}

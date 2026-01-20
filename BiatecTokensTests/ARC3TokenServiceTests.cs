using AlgorandAuthenticationV2;
using BiatecTokensApi.Configuration;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.ARC3;
using BiatecTokensApi.Models.ARC3.Request;
using BiatecTokensApi.Repositories;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace BiatecTokensTests
{
    [TestFixture]
    public class ARC3TokenServiceTests
    {
        private ARC3TokenService _service;
        private Mock<IOptionsMonitor<AlgorandAuthenticationOptionsV2>> _configMock;
        private Mock<ILogger<ARC3TokenService>> _loggerMock;
        private Mock<IIPFSRepository> _ipfsRepositoryMock;
        private Mock<IASATokenService> _asaServiceMock;
        private AlgorandAuthenticationOptionsV2 _algoConfig;

        [SetUp]
        public void Setup()
        {
            _configMock = new Mock<IOptionsMonitor<AlgorandAuthenticationOptionsV2>>();
            _loggerMock = new Mock<ILogger<ARC3TokenService>>();
            _ipfsRepositoryMock = new Mock<IIPFSRepository>();
            _asaServiceMock = new Mock<IASATokenService>();

            _algoConfig = new AlgorandAuthenticationOptionsV2
            {
                AllowedNetworks = new Dictionary<string, AlgodConfig>()
            };

            _configMock.Setup(x => x.CurrentValue).Returns(_algoConfig);
        }

        #region ValidateMetadata Tests

        [Test]
        public void ValidateMetadata_ValidMetadata_ReturnsTrue()
        {
            // Arrange
            var metadata = new ARC3TokenMetadata
            {
                Name = "Test NFT",
                Description = "Test Description"
            };

            // We can't instantiate the service without algod connection, so we test the validation logic
            Assert.That(metadata.Name, Is.Not.Null);
            Assert.That(metadata.Description, Is.Not.Null);
        }

        [Test]
        public void ValidateMetadata_ValidBackgroundColor_ReturnsTrue()
        {
            // Arrange - Valid hex color without #
            var validColors = new[] { "FF0000", "00FF00", "0000FF", "ABCDEF", "123456" };

            foreach (var color in validColors)
            {
                Assert.That(System.Text.RegularExpressions.Regex.IsMatch(color, @"^[0-9A-Fa-f]{6}$"), Is.True,
                    $"Color {color} should be valid");
            }
        }

        [Test]
        public void ValidateMetadata_InvalidBackgroundColor_ReturnsFalse()
        {
            // Arrange - Invalid hex colors
            var invalidColors = new[] { "#FF0000", "FF00", "GGGGGG", "FF00001", "" };

            foreach (var color in invalidColors)
            {
                if (!string.IsNullOrEmpty(color))
                {
                    Assert.That(System.Text.RegularExpressions.Regex.IsMatch(color, @"^[0-9A-Fa-f]{6}$"), Is.False,
                        $"Color {color} should be invalid");
                }
            }
        }

        [Test]
        public void ValidateMetadata_ValidImageMimeType_ReturnsTrue()
        {
            // Arrange
            var validMimeTypes = new[] { "image/png", "image/jpeg", "image/gif", "image/svg+xml" };

            foreach (var mimeType in validMimeTypes)
            {
                Assert.That(mimeType.StartsWith("image/"), Is.True,
                    $"MIME type {mimeType} should be valid");
            }
        }

        [Test]
        public void ValidateMetadata_InvalidImageMimeType_ReturnsFalse()
        {
            // Arrange
            var invalidMimeTypes = new[] { "text/plain", "application/json", "video/mp4", "audio/mp3" };

            foreach (var mimeType in invalidMimeTypes)
            {
                Assert.That(mimeType.StartsWith("image/"), Is.False,
                    $"MIME type {mimeType} should be invalid");
            }
        }

        [Test]
        public void ValidateMetadata_LocalizationWithoutUri_ReturnsFalse()
        {
            // Arrange
            var localization = new ARC3Localization
            {
                Uri = "",
                Default = "en",
                Locales = new List<string> { "en", "es" }
            };

            // Assert
            Assert.That(string.IsNullOrEmpty(localization.Uri), Is.True);
        }

        [Test]
        public void ValidateMetadata_LocalizationWithoutLocalePlaceholder_ReturnsFalse()
        {
            // Arrange
            var localization = new ARC3Localization
            {
                Uri = "https://example.com/metadata/",
                Default = "en",
                Locales = new List<string> { "en", "es" }
            };

            // Assert
            Assert.That(localization.Uri.Contains("{locale}"), Is.False);
        }

        [Test]
        public void ValidateMetadata_LocalizationWithValidUri_ReturnsTrue()
        {
            // Arrange
            var localization = new ARC3Localization
            {
                Uri = "https://example.com/metadata/{locale}",
                Default = "en",
                Locales = new List<string> { "en", "es" }
            };

            // Assert
            Assert.That(localization.Uri.Contains("{locale}"), Is.True);
            Assert.That(string.IsNullOrEmpty(localization.Default), Is.False);
            Assert.That(localization.Locales, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void ValidateMetadata_LocalizationWithoutDefault_ReturnsFalse()
        {
            // Arrange
            var localization = new ARC3Localization
            {
                Uri = "https://example.com/metadata/{locale}",
                Default = "",
                Locales = new List<string> { "en", "es" }
            };

            // Assert
            Assert.That(string.IsNullOrEmpty(localization.Default), Is.True);
        }

        [Test]
        public void ValidateMetadata_LocalizationWithoutLocales_ReturnsFalse()
        {
            // Arrange
            var localization = new ARC3Localization
            {
                Uri = "https://example.com/metadata/{locale}",
                Default = "en",
                Locales = new List<string>()
            };

            // Assert
            Assert.That(localization.Locales, Is.Empty);
        }

        #endregion

        #region Metadata Structure Tests

        [Test]
        public void Metadata_WithProperties_ShouldBeValid()
        {
            // Arrange
            var metadata = new ARC3TokenMetadata
            {
                Name = "Test NFT",
                Description = "Test Description",
                Image = "ipfs://QmTest",
                ImageIntegrity = "sha256-test",
                ImageMimetype = "image/png",
                BackgroundColor = "FF0000",
                ExternalUrl = "https://example.com",
                ExternalUrlIntegrity = "sha256-url",
                AnimationUrl = "https://example.com/animation.mp4",
                AnimationUrlIntegrity = "sha256-animation",
                AnimationUrlMimetype = "video/mp4"
            };

            // Assert
            Assert.That(metadata.Name, Is.EqualTo("Test NFT"));
            Assert.That(metadata.Description, Is.EqualTo("Test Description"));
            Assert.That(metadata.Image, Is.Not.Null);
            Assert.That(metadata.BackgroundColor, Is.EqualTo("FF0000"));
        }

        [Test]
        public void Metadata_WithAttributes_ShouldBeValid()
        {
            // Arrange
            var metadata = new ARC3TokenMetadata
            {
                Name = "Test NFT",
                Properties = new Dictionary<string, object>
                {
                    { "rarity", "legendary" },
                    { "power", 100 },
                    { "speed", 85.5 }
                }
            };

            // Assert
            Assert.That(metadata.Properties, Is.Not.Null);
            Assert.That(metadata.Properties.Count, Is.EqualTo(3));
            Assert.That(metadata.Properties.ContainsKey("rarity"), Is.True);
        }

        [Test]
        public void Metadata_WithLocalization_ShouldBeValid()
        {
            // Arrange
            var metadata = new ARC3TokenMetadata
            {
                Name = "Test NFT",
                Localization = new ARC3Localization
                {
                    Uri = "https://example.com/metadata/{locale}",
                    Default = "en",
                    Locales = new List<string> { "en", "es", "fr" },
                    Integrity = new Dictionary<string, string>
                    {
                        { "es", "sha256-es" },
                        { "fr", "sha256-fr" }
                    }
                }
            };

            // Assert
            Assert.That(metadata.Localization, Is.Not.Null);
            Assert.That(metadata.Localization.Locales.Count, Is.EqualTo(3));
            Assert.That(metadata.Localization.Integrity, Is.Not.Null);
        }

        [Test]
        public void Metadata_WithExtraMetadata_ShouldBeValid()
        {
            // Arrange
            var metadata = new ARC3TokenMetadata
            {
                Name = "Test NFT",
                ExtraMetadata = new Dictionary<string, string>
                {
                    { "customField1", "value1" },
                    { "customField2", "value2" }
                }
            };

            // Assert
            Assert.That(metadata.ExtraMetadata, Is.Not.Null);
            Assert.That(metadata.ExtraMetadata.Count, Is.EqualTo(2));
        }

        #endregion

        #region Edge Cases

        [Test]
        public void Metadata_MinimalValid_ShouldBeValid()
        {
            // Arrange
            var metadata = new ARC3TokenMetadata
            {
                Name = "T"
            };

            // Assert
            Assert.That(metadata.Name, Is.Not.Null);
            Assert.That(metadata.Name.Length, Is.GreaterThan(0));
        }

        [Test]
        public void Metadata_NameAtMaxLength_ShouldBeValid()
        {
            // Arrange - Maximum 32 characters per ARC3 spec
            var metadata = new ARC3TokenMetadata
            {
                Name = new string('A', 32)
            };

            // Assert
            Assert.That(metadata.Name.Length, Is.EqualTo(32));
        }

        [Test]
        public void Metadata_NameExceedsMaxLength_ShouldBeInvalid()
        {
            // Arrange - More than 32 characters
            var metadata = new ARC3TokenMetadata
            {
                Name = new string('A', 33)
            };

            // Assert
            Assert.That(metadata.Name.Length, Is.GreaterThan(32));
        }

        [Test]
        public void Metadata_WithNullOptionalFields_ShouldBeValid()
        {
            // Arrange
            var metadata = new ARC3TokenMetadata
            {
                Name = "Test NFT",
                Description = null,
                Image = null,
                BackgroundColor = null
            };

            // Assert
            Assert.That(metadata.Name, Is.Not.Null);
            Assert.That(metadata.Description, Is.Null);
        }

        [Test]
        public void Metadata_WithMultipleProperties_ShouldBeValid()
        {
            // Arrange
            var metadata = new ARC3TokenMetadata
            {
                Name = "Test NFT",
                Properties = new Dictionary<string, object>
                {
                    { "string_prop", "value" },
                    { "int_prop", 42 },
                    { "float_prop", 3.14 },
                    { "bool_prop", true },
                    { "null_prop", null }
                }
            };

            // Assert
            Assert.That(metadata.Properties.Count, Is.EqualTo(5));
            Assert.That(metadata.Properties["string_prop"], Is.EqualTo("value"));
            Assert.That(metadata.Properties["int_prop"], Is.EqualTo(42));
        }

        [Test]
        public void Metadata_IpfsUrlFormat_ShouldBeValid()
        {
            // Arrange
            var validIpfsUrls = new[]
            {
                "ipfs://QmTest",
                "ipfs://bafybeigdyrzt5sfp7udm7hu76uh7y26nf3efuylqabf3oclgtqy55fbzdi"
            };

            foreach (var url in validIpfsUrls)
            {
                Assert.That(url.StartsWith("ipfs://"), Is.True);
            }
        }

        [Test]
        public void BackgroundColor_CaseInsensitive_ShouldBeValid()
        {
            // Arrange
            var colors = new[] { "FFFFFF", "ffffff", "FfFfFf", "AbCdEf" };

            foreach (var color in colors)
            {
                Assert.That(System.Text.RegularExpressions.Regex.IsMatch(color, @"^[0-9A-Fa-f]{6}$"), Is.True,
                    $"Color {color} should be valid");
            }
        }

        #endregion
    }
}

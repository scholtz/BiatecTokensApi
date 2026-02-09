using BiatecTokensApi.Models;
using BiatecTokensApi.Models.TokenRegistry;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    [TestFixture]
    public class TokenMetadataServiceTests
    {
        private Mock<ILogger<TokenMetadataService>> _mockLogger;
        private Mock<ITokenRegistryService> _mockRegistryService;
        private TokenMetadataService _service;

        [SetUp]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger<TokenMetadataService>>();
            _mockRegistryService = new Mock<ITokenRegistryService>();
            _service = new TokenMetadataService(_mockLogger.Object, _mockRegistryService.Object);
        }

        [Test]
        public void CalculateCompletenessScore_WithAllFields_Returns100()
        {
            var metadata = new EnrichedTokenMetadata
            {
                Name = "Test Token",
                Symbol = "TEST",
                Decimals = 18,
                Description = "A test token",
                ImageUrl = "https://example.com/logo.png",
                WebsiteUrl = "https://example.com",
                ExplorerUrl = "https://explorer.example.com",
                DocumentationUrl = "https://docs.example.com",
                Tags = new List<string> { "test" },
                TokenIdentifier = "0x123",
                Chain = "ethereum-mainnet"
            };

            var score = _service.CalculateCompletenessScore(metadata);

            Assert.That(score, Is.EqualTo(100));
        }

        [Test]
        public void CalculateCompletenessScore_WithOnlyRequiredFields_ReturnsBaseScore()
        {
            var metadata = new EnrichedTokenMetadata
            {
                Name = "Test Token",
                Symbol = "TEST",
                Decimals = 18,
                TokenIdentifier = "0x123",
                Chain = "ethereum-mainnet"
            };

            var score = _service.CalculateCompletenessScore(metadata);

            Assert.That(score, Is.EqualTo(40));
        }

        [Test]
        public void GenerateExplorerUrl_ForAlgorandMainnet_ReturnsCorrectUrl()
        {
            var url = _service.GenerateExplorerUrl("123456", "algorand-mainnet");
            Assert.That(url, Is.EqualTo("https://explorer.perawallet.app/asset/123456"));
        }

        [Test]
        public void GenerateExplorerUrl_ForBaseMainnet_ReturnsCorrectUrl()
        {
            var url = _service.GenerateExplorerUrl("0xabc123", "base-mainnet");
            Assert.That(url, Is.EqualTo("https://basescan.org/token/0xabc123"));
        }

        [Test]
        public void GenerateExplorerUrl_ForEthereumMainnet_ReturnsCorrectUrl()
        {
            var url = _service.GenerateExplorerUrl("0xabc123", "ethereum-mainnet");
            Assert.That(url, Is.EqualTo("https://etherscan.io/token/0xabc123"));
        }

        [Test]
        public void GenerateExplorerUrl_ForUnsupportedChain_ReturnsNull()
        {
            var url = _service.GenerateExplorerUrl("123456", "unsupported-chain");
            Assert.That(url, Is.Null);
        }

        [Test]
        public async Task ValidateMetadataAsync_WithValidMetadata_ReturnsValidStatus()
        {
            var metadata = new EnrichedTokenMetadata
            {
                Name = "Test Token",
                Symbol = "TEST",
                Decimals = 18,
                Description = "A test token",
                ImageUrl = "https://example.com/logo.png",
                WebsiteUrl = "https://example.com",
                TokenIdentifier = "0x123",
                Chain = "ethereum-mainnet"
            };

            var result = await _service.ValidateMetadataAsync(metadata);

            Assert.That(result.ValidationStatus, Is.EqualTo(TokenMetadataValidationStatus.Valid));
            Assert.That(result.ValidationIssues.Count(i => i.Severity == TokenMetadataIssueSeverity.Error), Is.EqualTo(0));
        }

        [Test]
        public async Task ValidateMetadataAsync_WithMissingName_ReturnsInvalidWithError()
        {
            var metadata = new EnrichedTokenMetadata
            {
                Name = "",
                Symbol = "TEST",
                Decimals = 18,
                TokenIdentifier = "0x123",
                Chain = "ethereum-mainnet"
            };

            var result = await _service.ValidateMetadataAsync(metadata);

            Assert.That(result.ValidationStatus, Is.EqualTo(TokenMetadataValidationStatus.Invalid));
            Assert.That(result.ValidationIssues.Any(i => i.Code == "METADATA_001"), Is.True);
        }

        [Test]
        public async Task ValidateMetadataAsync_WithMissingOptionalFields_ReturnsValidWithWarnings()
        {
            var metadata = new EnrichedTokenMetadata
            {
                Name = "Test Token",
                Symbol = "TEST",
                Decimals = 18,
                TokenIdentifier = "0x123",
                Chain = "ethereum-mainnet"
            };

            var result = await _service.ValidateMetadataAsync(metadata);

            Assert.That(result.ValidationStatus, Is.EqualTo(TokenMetadataValidationStatus.ValidWithWarnings));
            Assert.That(result.ValidationIssues.Count(i => i.Severity == TokenMetadataIssueSeverity.Warning), Is.GreaterThan(0));
        }

        [Test]
        public void ApplyFallbacks_GeneratesDescriptionWhenMissing()
        {
            var metadata = new EnrichedTokenMetadata
            {
                Name = "Test Token",
                Symbol = "TEST",
                Decimals = 18,
                TokenIdentifier = "0x123",
                Chain = "ethereum-mainnet"
            };

            var result = _service.ApplyFallbacks(metadata);

            Assert.That(result.Description, Does.Contain("Test Token"));
            Assert.That(result.Description, Does.Contain("TEST"));
        }

        [Test]
        public async Task GetMetadataAsync_WhenTokenNotFound_ReturnsNull()
        {
            _mockRegistryService
                .Setup(s => s.GetTokenAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new GetTokenRegistryResponse { Found = false });

            var result = await _service.GetMetadataAsync("123456", "algorand-mainnet");

            Assert.That(result, Is.Null);
        }

        [Test]
        public async Task GetMetadataAsync_WhenTokenFound_ReturnsMetadata()
        {
            var registryEntry = new TokenRegistryEntry
            {
                Name = "Test Token",
                Symbol = "TEST",
                Decimals = 6,
                TokenIdentifier = "123456",
                Chain = "algorand-mainnet",
                Description = "A test token"
            };

            _mockRegistryService
                .Setup(s => s.GetTokenAsync("123456", "algorand-mainnet"))
                .ReturnsAsync(new GetTokenRegistryResponse
                {
                    Found = true,
                    Token = registryEntry
                });

            var result = await _service.GetMetadataAsync("123456", "algorand-mainnet");

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Name, Is.EqualTo("Test Token"));
            Assert.That(result.Symbol, Is.EqualTo("TEST"));
        }

        [Test]
        public async Task UpsertMetadataAsync_UpdatesLastUpdatedAt()
        {
            var originalTime = DateTime.UtcNow.AddDays(-1);
            var metadata = new EnrichedTokenMetadata
            {
                Name = "Test Token",
                Symbol = "TEST",
                Decimals = 18,
                TokenIdentifier = "0x123",
                Chain = "ethereum-mainnet",
                LastUpdatedAt = originalTime
            };

            var result = await _service.UpsertMetadataAsync(metadata);

            Assert.That(result.LastUpdatedAt, Is.GreaterThan(originalTime));
        }
    }
}

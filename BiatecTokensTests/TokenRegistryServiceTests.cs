using BiatecTokensApi.Models.TokenRegistry;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Unit tests for TokenRegistryService
    /// </summary>
    [TestFixture]
    public class TokenRegistryServiceTests
    {
        private Mock<ITokenRegistryRepository>? _mockRepository;
        private Mock<ILogger<TokenRegistryService>>? _mockLogger;
        private TokenRegistryService? _service;

        [SetUp]
        public void Setup()
        {
            _mockRepository = new Mock<ITokenRegistryRepository>();
            _mockLogger = new Mock<ILogger<TokenRegistryService>>();
            _service = new TokenRegistryService(_mockRepository.Object, _mockLogger.Object);
        }

        [Test]
        public async Task ListTokensAsync_ReturnsAllTokens_WhenNoFiltersApplied()
        {
            // Arrange
            var request = new ListTokenRegistryRequest
            {
                Page = 1,
                PageSize = 20
            };

            var expectedResponse = new ListTokenRegistryResponse
            {
                Tokens = new List<TokenRegistryEntry>
                {
                    new TokenRegistryEntry
                    {
                        Id = "1",
                        TokenIdentifier = "123456",
                        Chain = "algorand-mainnet",
                        Name = "Test Token",
                        Symbol = "TEST",
                        Decimals = 6
                    }
                },
                TotalCount = 1,
                Page = 1,
                PageSize = 20,
                TotalPages = 1
            };

            _mockRepository!
                .Setup(r => r.ListTokensAsync(It.IsAny<ListTokenRegistryRequest>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _service!.ListTokensAsync(request);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.TotalCount, Is.EqualTo(1));
            Assert.That(result.Tokens.Count, Is.EqualTo(1));
            Assert.That(result.Tokens[0].Symbol, Is.EqualTo("TEST"));
        }

        [Test]
        public async Task GetTokenAsync_ReturnsToken_WhenTokenExists()
        {
            // Arrange
            var tokenId = "123456";
            var expectedToken = new TokenRegistryEntry
            {
                Id = "1",
                TokenIdentifier = tokenId,
                Chain = "algorand-mainnet",
                Name = "Test Token",
                Symbol = "TEST"
            };

            _mockRepository!
                .Setup(r => r.GetTokenByIdAsync(tokenId, null))
                .ReturnsAsync(expectedToken);

            // Act
            var result = await _service!.GetTokenAsync(tokenId);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Found, Is.True);
            Assert.That(result.Token, Is.Not.Null);
            Assert.That(result.Token!.Symbol, Is.EqualTo("TEST"));
        }

        [Test]
        public async Task GetTokenAsync_ReturnsNotFound_WhenTokenDoesNotExist()
        {
            // Arrange
            var tokenId = "nonexistent";

            _mockRepository!
                .Setup(r => r.GetTokenByIdAsync(tokenId, null))
                .ReturnsAsync((TokenRegistryEntry?)null);

            // Act
            var result = await _service!.GetTokenAsync(tokenId);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Found, Is.False);
            Assert.That(result.Token, Is.Null);
            Assert.That(result.ErrorMessage, Does.Contain("not found").IgnoreCase);
        }

        [Test]
        public async Task UpsertTokenAsync_SuccessfullyCreatesToken_WithValidRequest()
        {
            // Arrange
            var request = new UpsertTokenRegistryRequest
            {
                TokenIdentifier = "123456",
                Chain = "algorand-mainnet",
                Name = "New Token",
                Symbol = "NEW",
                Decimals = 6
            };

            var expectedResponse = new UpsertTokenRegistryResponse
            {
                Success = true,
                Created = true,
                RegistryId = "generated-id",
                Token = new TokenRegistryEntry
                {
                    Id = "generated-id",
                    TokenIdentifier = request.TokenIdentifier,
                    Chain = request.Chain,
                    Name = request.Name,
                    Symbol = request.Symbol,
                    Decimals = request.Decimals
                }
            };

            _mockRepository!
                .Setup(r => r.UpsertTokenAsync(It.IsAny<UpsertTokenRegistryRequest>(), It.IsAny<string>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _service!.UpsertTokenAsync(request);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Success, Is.True);
            Assert.That(result.Created, Is.True);
            Assert.That(result.Token, Is.Not.Null);
            Assert.That(result.Token!.Symbol, Is.EqualTo("NEW"));
        }

        [Test]
        public async Task UpsertTokenAsync_FailsValidation_WhenRequiredFieldsMissing()
        {
            // Arrange
            var request = new UpsertTokenRegistryRequest
            {
                TokenIdentifier = "", // Missing
                Chain = "algorand-mainnet",
                Name = "New Token",
                Symbol = "NEW"
            };

            // Act
            var result = await _service!.UpsertTokenAsync(request);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Is.Not.Null);
            Assert.That(result.ErrorMessage, Does.Contain("required").IgnoreCase);
        }

        [Test]
        public async Task ValidateTokenAsync_ReturnsValid_ForValidToken()
        {
            // Arrange
            var token = new TokenRegistryEntry
            {
                TokenIdentifier = "123456",
                Chain = "algorand-mainnet",
                Name = "Test Token",
                Symbol = "TEST",
                Decimals = 6
            };

            // Act
            var result = await _service!.ValidateTokenAsync(token);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Errors, Is.Empty);
        }

        [Test]
        public async Task ValidateTokenAsync_ReturnsErrors_WhenRequiredFieldsMissing()
        {
            // Arrange
            var token = new TokenRegistryEntry
            {
                TokenIdentifier = "", // Missing
                Chain = "",
                Name = "",
                Symbol = ""
            };

            // Act
            var result = await _service!.ValidateTokenAsync(token);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors, Is.Not.Empty);
        }

        [Test]
        public async Task SearchTokensAsync_ReturnsMatchingTokens()
        {
            // Arrange
            var searchTerm = "TEST";
            var expectedTokens = new List<TokenRegistryEntry>
            {
                new TokenRegistryEntry
                {
                    Id = "1",
                    TokenIdentifier = "123456",
                    Chain = "algorand-mainnet",
                    Name = "Test Token",
                    Symbol = "TEST"
                }
            };

            _mockRepository!
                .Setup(r => r.SearchTokensAsync(searchTerm, It.IsAny<int>()))
                .ReturnsAsync(expectedTokens);

            // Act
            var result = await _service!.SearchTokensAsync(searchTerm);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].Symbol, Is.EqualTo("TEST"));
        }
    }
}

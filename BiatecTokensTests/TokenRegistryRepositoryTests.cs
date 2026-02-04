using BiatecTokensApi.Models.TokenRegistry;
using BiatecTokensApi.Repositories;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Unit tests for TokenRegistryRepository
    /// </summary>
    [TestFixture]
    public class TokenRegistryRepositoryTests
    {
        private Mock<ILogger<TokenRegistryRepository>>? _mockLogger;
        private TokenRegistryRepository? _repository;

        [SetUp]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger<TokenRegistryRepository>>();
            _repository = new TokenRegistryRepository(_mockLogger.Object);
        }

        [Test]
        public async Task UpsertTokenAsync_CreatesNewToken_WhenTokenDoesNotExist()
        {
            // Arrange
            var request = new UpsertTokenRegistryRequest
            {
                TokenIdentifier = "123456",
                Chain = "algorand-mainnet",
                Name = "Test Token",
                Symbol = "TEST",
                Decimals = 6,
                TotalSupply = "1000000"
            };

            // Act
            var result = await _repository!.UpsertTokenAsync(request, "creator-address");

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Success, Is.True);
            Assert.That(result.Created, Is.True);
            Assert.That(result.RegistryId, Is.Not.Null);
            Assert.That(result.Token, Is.Not.Null);
            Assert.That(result.Token!.Symbol, Is.EqualTo("TEST"));
            Assert.That(result.Token.Chain, Is.EqualTo("algorand-mainnet"));
        }

        [Test]
        public async Task UpsertTokenAsync_UpdatesExistingToken_WhenTokenExists()
        {
            // Arrange
            var request1 = new UpsertTokenRegistryRequest
            {
                TokenIdentifier = "123456",
                Chain = "algorand-mainnet",
                Name = "Test Token",
                Symbol = "TEST",
                Decimals = 6
            };

            var request2 = new UpsertTokenRegistryRequest
            {
                TokenIdentifier = "123456",
                Chain = "algorand-mainnet",
                Name = "Updated Token",
                Symbol = "TEST",
                Decimals = 8
            };

            // Act
            var result1 = await _repository!.UpsertTokenAsync(request1, "creator1");
            var result2 = await _repository.UpsertTokenAsync(request2, "creator2");

            // Assert
            Assert.That(result1.Created, Is.True);
            Assert.That(result2.Created, Is.False); // Should be an update
            Assert.That(result2.Token?.Name, Is.EqualTo("Updated Token"));
            Assert.That(result2.Token?.Decimals, Is.EqualTo(8));
        }

        [Test]
        public async Task GetTokenByIdAsync_ReturnsToken_WhenTokenExists()
        {
            // Arrange
            var request = new UpsertTokenRegistryRequest
            {
                TokenIdentifier = "123456",
                Chain = "algorand-mainnet",
                Name = "Test Token",
                Symbol = "TEST",
                Decimals = 6
            };

            var upsertResult = await _repository!.UpsertTokenAsync(request);
            var registryId = upsertResult.RegistryId!;

            // Act
            var token = await _repository.GetTokenByIdAsync(registryId);

            // Assert
            Assert.That(token, Is.Not.Null);
            Assert.That(token!.Id, Is.EqualTo(registryId));
            Assert.That(token.Symbol, Is.EqualTo("TEST"));
        }

        [Test]
        public async Task GetTokenByIdAsync_ReturnsNull_WhenTokenDoesNotExist()
        {
            // Act
            var token = await _repository!.GetTokenByIdAsync("nonexistent-id");

            // Assert
            Assert.That(token, Is.Null);
        }

        [Test]
        public async Task ListTokensAsync_ReturnsFilteredTokens_ByStandard()
        {
            // Arrange
            await _repository!.UpsertTokenAsync(new UpsertTokenRegistryRequest
            {
                TokenIdentifier = "1",
                Chain = "algorand-mainnet",
                Name = "ARC3 Token",
                Symbol = "ARC3",
                SupportedStandards = new List<string> { "ARC3" }
            });

            await _repository.UpsertTokenAsync(new UpsertTokenRegistryRequest
            {
                TokenIdentifier = "2",
                Chain = "algorand-mainnet",
                Name = "ERC20 Token",
                Symbol = "ERC20",
                SupportedStandards = new List<string> { "ERC20" }
            });

            var request = new ListTokenRegistryRequest
            {
                Standard = "ARC3",
                Page = 1,
                PageSize = 20
            };

            // Act
            var result = await _repository.ListTokensAsync(request);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.TotalCount, Is.EqualTo(1));
            Assert.That(result.Tokens.Count, Is.EqualTo(1));
            Assert.That(result.Tokens[0].Symbol, Is.EqualTo("ARC3"));
        }

        [Test]
        public async Task ListTokensAsync_ReturnsFilteredTokens_ByChain()
        {
            // Arrange
            await _repository!.UpsertTokenAsync(new UpsertTokenRegistryRequest
            {
                TokenIdentifier = "1",
                Chain = "algorand-mainnet",
                Name = "Algo Token",
                Symbol = "ALGO"
            });

            await _repository.UpsertTokenAsync(new UpsertTokenRegistryRequest
            {
                TokenIdentifier = "2",
                Chain = "base-mainnet",
                Name = "Base Token",
                Symbol = "BASE"
            });

            var request = new ListTokenRegistryRequest
            {
                Chain = "base-mainnet",
                Page = 1,
                PageSize = 20
            };

            // Act
            var result = await _repository.ListTokensAsync(request);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.TotalCount, Is.EqualTo(1));
            Assert.That(result.Tokens[0].Symbol, Is.EqualTo("BASE"));
        }

        [Test]
        public async Task ListTokensAsync_SupportsPagination()
        {
            // Arrange
            for (int i = 0; i < 30; i++)
            {
                await _repository!.UpsertTokenAsync(new UpsertTokenRegistryRequest
                {
                    TokenIdentifier = $"token-{i}",
                    Chain = "algorand-mainnet",
                    Name = $"Token {i}",
                    Symbol = $"TOK{i}"
                });
            }

            var requestPage1 = new ListTokenRegistryRequest { Page = 1, PageSize = 10 };
            var requestPage2 = new ListTokenRegistryRequest { Page = 2, PageSize = 10 };

            // Act
            var resultPage1 = await _repository!.ListTokensAsync(requestPage1);
            var resultPage2 = await _repository.ListTokensAsync(requestPage2);

            // Assert
            Assert.That(resultPage1.TotalCount, Is.EqualTo(30));
            Assert.That(resultPage1.Tokens.Count, Is.EqualTo(10));
            Assert.That(resultPage1.Page, Is.EqualTo(1));
            Assert.That(resultPage1.TotalPages, Is.EqualTo(3));
            Assert.That(resultPage1.HasNextPage, Is.True);
            Assert.That(resultPage1.HasPreviousPage, Is.False);

            Assert.That(resultPage2.Tokens.Count, Is.EqualTo(10));
            Assert.That(resultPage2.Page, Is.EqualTo(2));
            Assert.That(resultPage2.HasNextPage, Is.True);
            Assert.That(resultPage2.HasPreviousPage, Is.True);
        }

        [Test]
        public async Task SearchTokensAsync_ReturnsMatchingTokens_ByName()
        {
            // Arrange
            await _repository!.UpsertTokenAsync(new UpsertTokenRegistryRequest
            {
                TokenIdentifier = "1",
                Chain = "algorand-mainnet",
                Name = "Bitcoin Token",
                Symbol = "BTC"
            });

            await _repository.UpsertTokenAsync(new UpsertTokenRegistryRequest
            {
                TokenIdentifier = "2",
                Chain = "algorand-mainnet",
                Name = "Ethereum Token",
                Symbol = "ETH"
            });

            // Act
            var results = await _repository.SearchTokensAsync("Bitcoin");

            // Assert
            Assert.That(results, Is.Not.Null);
            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].Symbol, Is.EqualTo("BTC"));
        }

        [Test]
        public async Task DeleteTokenAsync_RemovesToken_WhenExists()
        {
            // Arrange
            var request = new UpsertTokenRegistryRequest
            {
                TokenIdentifier = "123456",
                Chain = "algorand-mainnet",
                Name = "Test Token",
                Symbol = "TEST"
            };

            var upsertResult = await _repository!.UpsertTokenAsync(request);
            var registryId = upsertResult.RegistryId!;

            // Act
            var deleted = await _repository.DeleteTokenAsync(registryId);
            var token = await _repository.GetTokenByIdAsync(registryId);

            // Assert
            Assert.That(deleted, Is.True);
            Assert.That(token, Is.Null);
        }
    }
}

using BiatecTokensApi.Models;
using BiatecTokensApi.Repositories;
using BiatecTokensApi.Repositories.Interface;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Tests for Token Issuance Audit Repository
    /// </summary>
    [TestFixture]
    public class TokenIssuanceAuditTests
    {
        private ITokenIssuanceRepository _repository = null!;
        private Mock<ILogger<TokenIssuanceRepository>> _loggerMock = null!;

        [SetUp]
        public void Setup()
        {
            _loggerMock = new Mock<ILogger<TokenIssuanceRepository>>();
            _repository = new TokenIssuanceRepository(_loggerMock.Object);
        }

        [Test]
        public async Task AddAuditLogEntryAsync_ShouldAddEntry()
        {
            // Arrange
            var entry = new TokenIssuanceAuditLogEntry
            {
                AssetId = 12345,
                Network = "voimain-v1.0",
                TokenType = "ASA_FT",
                TokenName = "Test Token",
                TokenSymbol = "TST",
                TotalSupply = "1000000",
                Decimals = 6,
                DeployedBy = "TEST_ADDRESS",
                Success = true,
                TransactionHash = "TXID123"
            };

            // Act
            await _repository.AddAuditLogEntryAsync(entry);
            var result = await _repository.GetAuditLogAsync(new GetTokenIssuanceAuditLogRequest());

            // Assert
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].AssetId, Is.EqualTo(12345));
            Assert.That(result[0].TokenName, Is.EqualTo("Test Token"));
        }

        [Test]
        public async Task GetAuditLogAsync_FilterByAssetId_ShouldReturnMatchingEntries()
        {
            // Arrange
            await _repository.AddAuditLogEntryAsync(new TokenIssuanceAuditLogEntry
            {
                AssetId = 111,
                Network = "voimain-v1.0",
                TokenType = "ASA_FT",
                TokenName = "Token 1",
                Success = true
            });

            await _repository.AddAuditLogEntryAsync(new TokenIssuanceAuditLogEntry
            {
                AssetId = 222,
                Network = "voimain-v1.0",
                TokenType = "ASA_FT",
                TokenName = "Token 2",
                Success = true
            });

            // Act
            var result = await _repository.GetAuditLogAsync(new GetTokenIssuanceAuditLogRequest
            {
                AssetId = 111
            });

            // Assert
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].AssetId, Is.EqualTo(111));
            Assert.That(result[0].TokenName, Is.EqualTo("Token 1"));
        }

        [Test]
        public async Task GetAuditLogAsync_FilterByNetwork_ShouldReturnMatchingEntries()
        {
            // Arrange
            await _repository.AddAuditLogEntryAsync(new TokenIssuanceAuditLogEntry
            {
                AssetId = 111,
                Network = "voimain-v1.0",
                TokenType = "ASA_FT",
                TokenName = "VOI Token",
                Success = true
            });

            await _repository.AddAuditLogEntryAsync(new TokenIssuanceAuditLogEntry
            {
                AssetId = 222,
                Network = "aramidmain-v1.0",
                TokenType = "ASA_FT",
                TokenName = "Aramid Token",
                Success = true
            });

            // Act
            var result = await _repository.GetAuditLogAsync(new GetTokenIssuanceAuditLogRequest
            {
                Network = "aramidmain-v1.0"
            });

            // Assert
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].Network, Is.EqualTo("aramidmain-v1.0"));
            Assert.That(result[0].TokenName, Is.EqualTo("Aramid Token"));
        }

        [Test]
        public async Task GetAuditLogAsync_FilterBySuccess_ShouldReturnMatchingEntries()
        {
            // Arrange
            await _repository.AddAuditLogEntryAsync(new TokenIssuanceAuditLogEntry
            {
                AssetId = 111,
                Network = "voimain-v1.0",
                TokenName = "Success Token",
                Success = true
            });

            await _repository.AddAuditLogEntryAsync(new TokenIssuanceAuditLogEntry
            {
                Network = "voimain-v1.0",
                TokenName = "Failed Token",
                Success = false,
                ErrorMessage = "Deployment failed"
            });

            // Act
            var successResult = await _repository.GetAuditLogAsync(new GetTokenIssuanceAuditLogRequest
            {
                Success = true
            });

            var failResult = await _repository.GetAuditLogAsync(new GetTokenIssuanceAuditLogRequest
            {
                Success = false
            });

            // Assert
            Assert.That(successResult.Count, Is.EqualTo(1));
            Assert.That(successResult[0].Success, Is.True);
            Assert.That(failResult.Count, Is.EqualTo(1));
            Assert.That(failResult[0].Success, Is.False);
        }

        [Test]
        public async Task GetAuditLogCountAsync_ShouldReturnCorrectCount()
        {
            // Arrange
            await _repository.AddAuditLogEntryAsync(new TokenIssuanceAuditLogEntry
            {
                AssetId = 111,
                Network = "voimain-v1.0",
                Success = true
            });

            await _repository.AddAuditLogEntryAsync(new TokenIssuanceAuditLogEntry
            {
                AssetId = 222,
                Network = "voimain-v1.0",
                Success = true
            });

            // Act
            var count = await _repository.GetAuditLogCountAsync(new GetTokenIssuanceAuditLogRequest());

            // Assert
            Assert.That(count, Is.EqualTo(2));
        }

        [Test]
        public async Task GetAuditLogAsync_Pagination_ShouldWorkCorrectly()
        {
            // Arrange
            for (int i = 1; i <= 10; i++)
            {
                await _repository.AddAuditLogEntryAsync(new TokenIssuanceAuditLogEntry
                {
                    AssetId = (ulong)i,
                    Network = "voimain-v1.0",
                    TokenName = $"Token {i}",
                    Success = true
                });
            }

            // Act
            var page1 = await _repository.GetAuditLogAsync(new GetTokenIssuanceAuditLogRequest
            {
                Page = 1,
                PageSize = 3
            });

            var page2 = await _repository.GetAuditLogAsync(new GetTokenIssuanceAuditLogRequest
            {
                Page = 2,
                PageSize = 3
            });

            // Assert
            Assert.That(page1.Count, Is.EqualTo(3));
            Assert.That(page2.Count, Is.EqualTo(3));
            // Ensure no overlap
            Assert.That(page1[0].AssetId, Is.Not.EqualTo(page2[0].AssetId));
        }
    }
}

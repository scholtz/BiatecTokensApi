using BiatecTokensApi.Models.Whitelist;
using BiatecTokensApi.Repositories;
using Microsoft.Extensions.Logging;
using Moq;

namespace BiatecTokensTests
{
    [TestFixture]
    public class WhitelistRepositoryTests
    {
        private WhitelistRepository _repository;
        private Mock<ILogger<WhitelistRepository>> _loggerMock;

        [SetUp]
        public void Setup()
        {
            _loggerMock = new Mock<ILogger<WhitelistRepository>>();
            _repository = new WhitelistRepository(_loggerMock.Object);
        }

        [Test]
        public async Task AddEntryAsync_NewEntry_ShouldSucceed()
        {
            // Arrange
            var entry = new WhitelistEntry
            {
                AssetId = 12345,
                Address = "TESTADDRESS123456789012345678901234567890123456789012",
                Status = WhitelistStatus.Active,
                CreatedBy = "CREATOR123456789012345678901234567890123456789012345"
            };

            // Act
            var result = await _repository.AddEntryAsync(entry);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public async Task AddEntryAsync_DuplicateEntry_ShouldFail()
        {
            // Arrange
            var entry = new WhitelistEntry
            {
                AssetId = 12345,
                Address = "TESTADDRESS123456789012345678901234567890123456789012",
                Status = WhitelistStatus.Active,
                CreatedBy = "CREATOR123456789012345678901234567890123456789012345"
            };

            // Act
            var firstAdd = await _repository.AddEntryAsync(entry);
            var secondAdd = await _repository.AddEntryAsync(entry);

            // Assert
            Assert.That(firstAdd, Is.True);
            Assert.That(secondAdd, Is.False);
        }

        [Test]
        public async Task GetEntryAsync_ExistingEntry_ShouldReturnEntry()
        {
            // Arrange
            var entry = new WhitelistEntry
            {
                AssetId = 12345,
                Address = "TESTADDRESS123456789012345678901234567890123456789012",
                Status = WhitelistStatus.Active,
                CreatedBy = "CREATOR123456789012345678901234567890123456789012345"
            };
            await _repository.AddEntryAsync(entry);

            // Act
            var result = await _repository.GetEntryAsync(12345, "TESTADDRESS123456789012345678901234567890123456789012");

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result?.AssetId, Is.EqualTo(12345));
            Assert.That(result?.Address, Is.EqualTo("TESTADDRESS123456789012345678901234567890123456789012"));
        }

        [Test]
        public async Task GetEntryAsync_NonExistentEntry_ShouldReturnNull()
        {
            // Act
            var result = await _repository.GetEntryAsync(12345, "NONEXISTENT12345678901234567890123456789012345678901");

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public async Task GetEntriesByAssetIdAsync_MultipleEntries_ShouldReturnAll()
        {
            // Arrange
            var entry1 = new WhitelistEntry
            {
                AssetId = 12345,
                Address = "ADDRESS1123456789012345678901234567890123456789012345",
                Status = WhitelistStatus.Active,
                CreatedBy = "CREATOR123456789012345678901234567890123456789012345"
            };
            var entry2 = new WhitelistEntry
            {
                AssetId = 12345,
                Address = "ADDRESS2123456789012345678901234567890123456789012345",
                Status = WhitelistStatus.Active,
                CreatedBy = "CREATOR123456789012345678901234567890123456789012345"
            };
            var entry3 = new WhitelistEntry
            {
                AssetId = 67890,
                Address = "ADDRESS3123456789012345678901234567890123456789012345",
                Status = WhitelistStatus.Active,
                CreatedBy = "CREATOR123456789012345678901234567890123456789012345"
            };

            await _repository.AddEntryAsync(entry1);
            await _repository.AddEntryAsync(entry2);
            await _repository.AddEntryAsync(entry3);

            // Act
            var result = await _repository.GetEntriesByAssetIdAsync(12345);

            // Assert
            Assert.That(result, Has.Count.EqualTo(2));
            Assert.That(result.All(e => e.AssetId == 12345), Is.True);
        }

        [Test]
        public async Task GetEntriesByAssetIdAsync_WithStatusFilter_ShouldReturnFilteredEntries()
        {
            // Arrange
            var entry1 = new WhitelistEntry
            {
                AssetId = 12345,
                Address = "ADDRESS1123456789012345678901234567890123456789012345",
                Status = WhitelistStatus.Active,
                CreatedBy = "CREATOR123456789012345678901234567890123456789012345"
            };
            var entry2 = new WhitelistEntry
            {
                AssetId = 12345,
                Address = "ADDRESS2123456789012345678901234567890123456789012345",
                Status = WhitelistStatus.Inactive,
                CreatedBy = "CREATOR123456789012345678901234567890123456789012345"
            };

            await _repository.AddEntryAsync(entry1);
            await _repository.AddEntryAsync(entry2);

            // Act
            var activeResults = await _repository.GetEntriesByAssetIdAsync(12345, WhitelistStatus.Active);
            var inactiveResults = await _repository.GetEntriesByAssetIdAsync(12345, WhitelistStatus.Inactive);

            // Assert
            Assert.That(activeResults, Has.Count.EqualTo(1));
            Assert.That(activeResults[0].Status, Is.EqualTo(WhitelistStatus.Active));
            Assert.That(inactiveResults, Has.Count.EqualTo(1));
            Assert.That(inactiveResults[0].Status, Is.EqualTo(WhitelistStatus.Inactive));
        }

        [Test]
        public async Task UpdateEntryAsync_ExistingEntry_ShouldSucceed()
        {
            // Arrange
            var entry = new WhitelistEntry
            {
                AssetId = 12345,
                Address = "TESTADDRESS123456789012345678901234567890123456789012",
                Status = WhitelistStatus.Active,
                CreatedBy = "CREATOR123456789012345678901234567890123456789012345"
            };
            await _repository.AddEntryAsync(entry);

            // Act
            entry.Status = WhitelistStatus.Inactive;
            var result = await _repository.UpdateEntryAsync(entry);

            // Assert
            Assert.That(result, Is.True);
            var updatedEntry = await _repository.GetEntryAsync(12345, "TESTADDRESS123456789012345678901234567890123456789012");
            Assert.That(updatedEntry?.Status, Is.EqualTo(WhitelistStatus.Inactive));
        }

        [Test]
        public async Task UpdateEntryAsync_NonExistentEntry_ShouldFail()
        {
            // Arrange
            var entry = new WhitelistEntry
            {
                AssetId = 12345,
                Address = "NONEXISTENT12345678901234567890123456789012345678901",
                Status = WhitelistStatus.Active,
                CreatedBy = "CREATOR123456789012345678901234567890123456789012345"
            };

            // Act
            var result = await _repository.UpdateEntryAsync(entry);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public async Task RemoveEntryAsync_ExistingEntry_ShouldSucceed()
        {
            // Arrange
            var entry = new WhitelistEntry
            {
                AssetId = 12345,
                Address = "TESTADDRESS123456789012345678901234567890123456789012",
                Status = WhitelistStatus.Active,
                CreatedBy = "CREATOR123456789012345678901234567890123456789012345"
            };
            await _repository.AddEntryAsync(entry);

            // Act
            var result = await _repository.RemoveEntryAsync(12345, "TESTADDRESS123456789012345678901234567890123456789012");

            // Assert
            Assert.That(result, Is.True);
            var deletedEntry = await _repository.GetEntryAsync(12345, "TESTADDRESS123456789012345678901234567890123456789012");
            Assert.That(deletedEntry, Is.Null);
        }

        [Test]
        public async Task RemoveEntryAsync_NonExistentEntry_ShouldFail()
        {
            // Act
            var result = await _repository.RemoveEntryAsync(12345, "NONEXISTENT12345678901234567890123456789012345678901");

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public async Task IsWhitelistedAsync_ActiveEntry_ShouldReturnTrue()
        {
            // Arrange
            var entry = new WhitelistEntry
            {
                AssetId = 12345,
                Address = "TESTADDRESS123456789012345678901234567890123456789012",
                Status = WhitelistStatus.Active,
                CreatedBy = "CREATOR123456789012345678901234567890123456789012345"
            };
            await _repository.AddEntryAsync(entry);

            // Act
            var result = await _repository.IsWhitelistedAsync(12345, "TESTADDRESS123456789012345678901234567890123456789012");

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public async Task IsWhitelistedAsync_InactiveEntry_ShouldReturnFalse()
        {
            // Arrange
            var entry = new WhitelistEntry
            {
                AssetId = 12345,
                Address = "TESTADDRESS123456789012345678901234567890123456789012",
                Status = WhitelistStatus.Inactive,
                CreatedBy = "CREATOR123456789012345678901234567890123456789012345"
            };
            await _repository.AddEntryAsync(entry);

            // Act
            var result = await _repository.IsWhitelistedAsync(12345, "TESTADDRESS123456789012345678901234567890123456789012");

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public async Task IsWhitelistedAsync_NonExistentEntry_ShouldReturnFalse()
        {
            // Act
            var result = await _repository.IsWhitelistedAsync(12345, "NONEXISTENT12345678901234567890123456789012345678901");

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public async Task AddEntryAsync_CaseInsensitiveAddress_ShouldTreatAsSame()
        {
            // Arrange
            var entry1 = new WhitelistEntry
            {
                AssetId = 12345,
                Address = "testaddress123456789012345678901234567890123456789012",
                Status = WhitelistStatus.Active,
                CreatedBy = "CREATOR123456789012345678901234567890123456789012345"
            };
            var entry2 = new WhitelistEntry
            {
                AssetId = 12345,
                Address = "TESTADDRESS123456789012345678901234567890123456789012",
                Status = WhitelistStatus.Active,
                CreatedBy = "CREATOR123456789012345678901234567890123456789012345"
            };

            // Act
            var firstAdd = await _repository.AddEntryAsync(entry1);
            var secondAdd = await _repository.AddEntryAsync(entry2);

            // Assert
            Assert.That(firstAdd, Is.True);
            Assert.That(secondAdd, Is.False);
        }
    }
}

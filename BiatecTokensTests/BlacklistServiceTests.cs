using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Models.Whitelist;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Unit tests for blacklist management functionality
    /// </summary>
    [TestFixture]
    public class BlacklistServiceTests
    {
        private Mock<IComplianceRepository> _mockRepository;
        private Mock<IWhitelistService> _mockWhitelistService;
        private Mock<ILogger<ComplianceService>> _mockLogger;
        private Mock<ISubscriptionMeteringService> _mockMeteringService;
        private ComplianceService _service;

        [SetUp]
        public void Setup()
        {
            _mockRepository = new Mock<IComplianceRepository>();
            _mockWhitelistService = new Mock<IWhitelistService>();
            _mockLogger = new Mock<ILogger<ComplianceService>>();
            _mockMeteringService = new Mock<ISubscriptionMeteringService>();
            _service = new ComplianceService(
                _mockRepository.Object,
                _mockWhitelistService.Object,
                _mockLogger.Object,
                _mockMeteringService.Object);
        }

        [Test]
        public async Task AddBlacklistEntry_ValidRequest_ShouldSucceed()
        {
            // Arrange
            var createdBy = "CREATORADDRESS123456789012345678901234567890AB";
            var request = new AddBlacklistEntryRequest
            {
                Address = "BLACKLISTEDADDRESS123456789012345678901234567",
                Reason = "OFAC sanctions list",
                Category = BlacklistCategory.Sanctions,
                Network = "voimain-v1.0"
            };

            _mockRepository.Setup(r => r.CreateBlacklistEntryAsync(It.IsAny<BlacklistEntry>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.AddBlacklistEntryAsync(request, createdBy);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Entry, Is.Not.Null);
            Assert.That(result.Entry!.Address, Is.EqualTo(request.Address));
            Assert.That(result.Entry.Category, Is.EqualTo(BlacklistCategory.Sanctions));
            Assert.That(result.Entry.Status, Is.EqualTo(BlacklistStatus.Active));
            Assert.That(result.Entry.CreatedBy, Is.EqualTo(createdBy));
            _mockRepository.Verify(r => r.CreateBlacklistEntryAsync(It.IsAny<BlacklistEntry>()), Times.Once);
        }

        [Test]
        public async Task CheckBlacklist_BlacklistedAddress_ShouldReturnTrue()
        {
            // Arrange
            var address = "BLACKLISTEDADDRESS123456789012345678901234567";
            var assetId = 12345ul;
            var request = new CheckBlacklistRequest
            {
                Address = address,
                AssetId = assetId
            };

            var blacklistEntry = new BlacklistEntry
            {
                Id = Guid.NewGuid().ToString(),
                Address = address,
                AssetId = assetId,
                Category = BlacklistCategory.Sanctions,
                Status = BlacklistStatus.Active,
                EffectiveDate = DateTime.UtcNow.AddDays(-1)
            };

            _mockRepository.Setup(r => r.CheckBlacklistAsync(address, assetId, null))
                .ReturnsAsync(new List<BlacklistEntry> { blacklistEntry });

            // Act
            var result = await _service.CheckBlacklistAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.IsBlacklisted, Is.True);
            Assert.That(result.Entries, Has.Count.EqualTo(1));
            Assert.That(result.AssetSpecificBlacklist, Is.True);
        }

        [Test]
        public async Task CheckBlacklist_NonBlacklistedAddress_ShouldReturnFalse()
        {
            // Arrange
            var address = "CLEANADDRESS123456789012345678901234567890ABCD";
            var request = new CheckBlacklistRequest
            {
                Address = address
            };

            _mockRepository.Setup(r => r.CheckBlacklistAsync(address, null, null))
                .ReturnsAsync(new List<BlacklistEntry>());

            // Act
            var result = await _service.CheckBlacklistAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.IsBlacklisted, Is.False);
            Assert.That(result.Entries, Is.Empty);
        }

        [Test]
        public async Task ListBlacklistEntries_WithFilters_ShouldReturnFiltered()
        {
            // Arrange
            var request = new ListBlacklistEntriesRequest
            {
                Category = BlacklistCategory.Sanctions,
                Status = BlacklistStatus.Active,
                Page = 1,
                PageSize = 20
            };

            var entries = new List<BlacklistEntry>
            {
                new BlacklistEntry
                {
                    Id = "1",
                    Address = "ADDR1",
                    Category = BlacklistCategory.Sanctions,
                    Status = BlacklistStatus.Active
                },
                new BlacklistEntry
                {
                    Id = "2",
                    Address = "ADDR2",
                    Category = BlacklistCategory.Sanctions,
                    Status = BlacklistStatus.Active
                }
            };

            _mockRepository.Setup(r => r.ListBlacklistEntriesAsync(request))
                .ReturnsAsync(entries);
            _mockRepository.Setup(r => r.GetBlacklistEntryCountAsync(request))
                .ReturnsAsync(2);

            // Act
            var result = await _service.ListBlacklistEntriesAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Entries, Has.Count.EqualTo(2));
            Assert.That(result.TotalCount, Is.EqualTo(2));
        }

        [Test]
        public async Task DeleteBlacklistEntry_ExistingEntry_ShouldSucceed()
        {
            // Arrange
            var entryId = Guid.NewGuid().ToString();

            _mockRepository.Setup(r => r.DeleteBlacklistEntryAsync(entryId))
                .ReturnsAsync(true);

            // Act
            var result = await _service.DeleteBlacklistEntryAsync(entryId);

            // Assert
            Assert.That(result.Success, Is.True);
            _mockRepository.Verify(r => r.DeleteBlacklistEntryAsync(entryId), Times.Once);
        }

        [Test]
        public async Task DeleteBlacklistEntry_NonExistent_ShouldFail()
        {
            // Arrange
            var entryId = "nonexistent-id";

            _mockRepository.Setup(r => r.DeleteBlacklistEntryAsync(entryId))
                .ReturnsAsync(false);

            // Act
            var result = await _service.DeleteBlacklistEntryAsync(entryId);

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Is.EqualTo("Blacklist entry not found"));
        }

        [Test]
        public async Task ValidateTransfer_BothWhitelisted_ShouldAllow()
        {
            // Arrange
            var request = new ValidateComplianceTransferRequest
            {
                AssetId = 12345,
                FromAddress = "SENDER123456789012345678901234567890ABCDEFGH",
                ToAddress = "RECEIVER123456789012345678901234567890ABCDEF",
                Amount = 1000,
                Network = "voimain-v1.0"
            };

            var whitelistEntries = new List<WhitelistEntry>
            {
                new WhitelistEntry { Address = request.FromAddress, Status = WhitelistStatus.Active },
                new WhitelistEntry { Address = request.ToAddress, Status = WhitelistStatus.Active }
            };

            _mockWhitelistService.Setup(w => w.ListEntriesAsync(It.IsAny<ListWhitelistRequest>()))
                .ReturnsAsync(new WhitelistListResponse
                {
                    Success = true,
                    Entries = whitelistEntries
                });

            _mockRepository.Setup(r => r.CheckBlacklistAsync(It.IsAny<string>(), It.IsAny<ulong?>(), It.IsAny<string?>()))
                .ReturnsAsync(new List<BlacklistEntry>());

            _mockRepository.Setup(r => r.GetMetadataByAssetIdAsync(request.AssetId))
                .ReturnsAsync((ComplianceMetadata?)null);

            // Act
            var result = await _service.ValidateTransferAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.CanTransfer, Is.True);
            Assert.That(result.Violations, Is.Empty);
        }

        [Test]
        public async Task ValidateTransfer_SenderBlacklisted_ShouldDeny()
        {
            // Arrange
            var request = new ValidateComplianceTransferRequest
            {
                AssetId = 12345,
                FromAddress = "BLACKLISTED123456789012345678901234567890AB",
                ToAddress = "RECEIVER123456789012345678901234567890ABCDEF",
                Amount = 1000
            };

            var whitelistEntries = new List<WhitelistEntry>
            {
                new WhitelistEntry { Address = request.FromAddress, Status = WhitelistStatus.Active },
                new WhitelistEntry { Address = request.ToAddress, Status = WhitelistStatus.Active }
            };

            _mockWhitelistService.Setup(w => w.ListEntriesAsync(It.IsAny<ListWhitelistRequest>()))
                .ReturnsAsync(new WhitelistListResponse
                {
                    Success = true,
                    Entries = whitelistEntries
                });

            var blacklistEntry = new BlacklistEntry
            {
                Address = request.FromAddress,
                Reason = "OFAC sanctions",
                Status = BlacklistStatus.Active,
                EffectiveDate = DateTime.UtcNow.AddDays(-1)
            };

            _mockRepository.Setup(r => r.CheckBlacklistAsync(request.FromAddress, It.IsAny<ulong?>(), It.IsAny<string?>()))
                .ReturnsAsync(new List<BlacklistEntry> { blacklistEntry });

            _mockRepository.Setup(r => r.CheckBlacklistAsync(request.ToAddress, It.IsAny<ulong?>(), It.IsAny<string?>()))
                .ReturnsAsync(new List<BlacklistEntry>());

            _mockRepository.Setup(r => r.GetMetadataByAssetIdAsync(request.AssetId))
                .ReturnsAsync((ComplianceMetadata?)null);

            // Act
            var result = await _service.ValidateTransferAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.CanTransfer, Is.False);
            Assert.That(result.Violations, Is.Not.Empty);
            Assert.That(result.Violations, Contains.Item("Sender address is blacklisted: OFAC sanctions"));
        }

        [Test]
        public async Task ValidateTransfer_ReceiverNotWhitelisted_ShouldDeny()
        {
            // Arrange
            var request = new ValidateComplianceTransferRequest
            {
                AssetId = 12345,
                FromAddress = "SENDER123456789012345678901234567890ABCDEFGH",
                ToAddress = "NOTWHITELISTED123456789012345678901234567890",
                Amount = 1000
            };

            var whitelistEntries = new List<WhitelistEntry>
            {
                new WhitelistEntry { Address = request.FromAddress, Status = WhitelistStatus.Active }
                // ToAddress is not in whitelist
            };

            _mockWhitelistService.Setup(w => w.ListEntriesAsync(It.IsAny<ListWhitelistRequest>()))
                .ReturnsAsync(new WhitelistListResponse
                {
                    Success = true,
                    Entries = whitelistEntries
                });

            _mockRepository.Setup(r => r.CheckBlacklistAsync(It.IsAny<string>(), It.IsAny<ulong?>(), It.IsAny<string?>()))
                .ReturnsAsync(new List<BlacklistEntry>());

            _mockRepository.Setup(r => r.GetMetadataByAssetIdAsync(request.AssetId))
                .ReturnsAsync((ComplianceMetadata?)null);

            // Act
            var result = await _service.ValidateTransferAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.CanTransfer, Is.False);
            Assert.That(result.Violations, Contains.Item("Receiver address is not whitelisted for this token"));
        }
    }
}

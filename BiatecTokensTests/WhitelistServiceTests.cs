using BiatecTokensApi.Models.Metering;
using BiatecTokensApi.Models.Whitelist;
using BiatecTokensApi.Repositories;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging;
using Moq;

namespace BiatecTokensTests
{
    [TestFixture]
    public class WhitelistServiceTests
    {
        private Mock<IWhitelistRepository> _repositoryMock;
        private Mock<ILogger<WhitelistService>> _loggerMock;
        private Mock<ISubscriptionMeteringService> _meteringServiceMock;
        private WhitelistService _service;

        [SetUp]
        public void Setup()
        {
            _repositoryMock = new Mock<IWhitelistRepository>();
            _loggerMock = new Mock<ILogger<WhitelistService>>();
            _meteringServiceMock = new Mock<ISubscriptionMeteringService>();
            _service = new WhitelistService(_repositoryMock.Object, _loggerMock.Object, _meteringServiceMock.Object);
        }

        #region Address Validation Tests

        [Test]
        public void IsValidAlgorandAddress_ValidAddress_ShouldReturnTrue()
        {
            // Arrange - Valid Algorand address (58 characters)
            var validAddress = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";

            // Act
            var result = _service.IsValidAlgorandAddress(validAddress);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void IsValidAlgorandAddress_InvalidLength_ShouldReturnFalse()
        {
            // Arrange - Too short
            var invalidAddress = "SHORT";

            // Act
            var result = _service.IsValidAlgorandAddress(invalidAddress);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void IsValidAlgorandAddress_EmptyString_ShouldReturnFalse()
        {
            // Act
            var result = _service.IsValidAlgorandAddress(string.Empty);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void IsValidAlgorandAddress_Null_ShouldReturnFalse()
        {
            // Act
            var result = _service.IsValidAlgorandAddress(null!);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void IsValidAlgorandAddress_InvalidChecksum_ShouldReturnFalse()
        {
            // Arrange - Invalid checksum (all A's is not a valid address)
            var invalidAddress = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

            // Act
            var result = _service.IsValidAlgorandAddress(invalidAddress);

            // Assert
            Assert.That(result, Is.False);
        }

        #endregion

        #region AddEntry Tests

        [Test]
        public async Task AddEntryAsync_ValidRequest_ShouldSucceed()
        {
            // Arrange
            var request = new AddWhitelistEntryRequest
            {
                AssetId = 12345,
                Address = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA",
                Status = WhitelistStatus.Active
            };
            var createdBy = "CREATOR1AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";

            _repositoryMock.Setup(r => r.GetEntryAsync(It.IsAny<ulong>(), It.IsAny<string>()))
                .ReturnsAsync((WhitelistEntry?)null);
            _repositoryMock.Setup(r => r.AddEntryAsync(It.IsAny<WhitelistEntry>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.AddEntryAsync(request, createdBy);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Entry, Is.Not.Null);
            Assert.That(result.Entry?.AssetId, Is.EqualTo(12345));
            Assert.That(result.Entry?.CreatedBy, Is.EqualTo(createdBy));
        }

        [Test]
        public async Task AddEntryAsync_InvalidAddress_ShouldFail()
        {
            // Arrange
            var request = new AddWhitelistEntryRequest
            {
                AssetId = 12345,
                Address = "INVALID",
                Status = WhitelistStatus.Active
            };
            var createdBy = "CREATOR1AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";

            // Act
            var result = await _service.AddEntryAsync(request, createdBy);

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("Invalid Algorand address"));
        }

        [Test]
        public async Task AddEntryAsync_ExistingEntry_ShouldUpdateStatus()
        {
            // Arrange
            var request = new AddWhitelistEntryRequest
            {
                AssetId = 12345,
                Address = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA",
                Status = WhitelistStatus.Active
            };
            var createdBy = "CREATOR1AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";

            var existingEntry = new WhitelistEntry
            {
                AssetId = 12345,
                Address = request.Address.ToUpperInvariant(),
                Status = WhitelistStatus.Inactive,
                CreatedBy = "OLDCREATOR",
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            };

            _repositoryMock.Setup(r => r.GetEntryAsync(It.IsAny<ulong>(), It.IsAny<string>()))
                .ReturnsAsync(existingEntry);
            _repositoryMock.Setup(r => r.UpdateEntryAsync(It.IsAny<WhitelistEntry>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.AddEntryAsync(request, createdBy);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Entry?.Status, Is.EqualTo(WhitelistStatus.Active));
            Assert.That(result.Entry?.UpdatedBy, Is.EqualTo(createdBy));
            Assert.That(result.Entry?.UpdatedAt, Is.Not.Null);
        }

        #endregion

        #region RemoveEntry Tests

        [Test]
        public async Task RemoveEntryAsync_ValidRequest_ShouldSucceed()
        {
            // Arrange
            var request = new RemoveWhitelistEntryRequest
            {
                AssetId = 12345,
                Address = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA"
            };

            _repositoryMock.Setup(r => r.RemoveEntryAsync(It.IsAny<ulong>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.RemoveEntryAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
        }

        [Test]
        public async Task RemoveEntryAsync_InvalidAddress_ShouldFail()
        {
            // Arrange
            var request = new RemoveWhitelistEntryRequest
            {
                AssetId = 12345,
                Address = "INVALID"
            };

            // Act
            var result = await _service.RemoveEntryAsync(request);

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("Invalid Algorand address"));
        }

        [Test]
        public async Task RemoveEntryAsync_NonExistentEntry_ShouldFail()
        {
            // Arrange
            var request = new RemoveWhitelistEntryRequest
            {
                AssetId = 12345,
                Address = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA"
            };

            _repositoryMock.Setup(r => r.RemoveEntryAsync(It.IsAny<ulong>(), It.IsAny<string>()))
                .ReturnsAsync(false);

            // Act
            var result = await _service.RemoveEntryAsync(request);

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Is.EqualTo("Whitelist entry not found"));
        }

        #endregion

        #region BulkAdd Tests

        [Test]
        public async Task BulkAddEntriesAsync_AllValidAddresses_ShouldSucceed()
        {
            // Arrange - Use valid Algorand testnet addresses  
            // These are real Algorand addresses from testnet
            var request = new BulkAddWhitelistRequest
            {
                AssetId = 12345,
                Addresses = new List<string>
                {
                    "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA", // Valid address
                    "47YPQTIGQEO7T4Y4RWDYWEKV6RTR2UNBQXBABEEGM72ESWDQNCQ52OPASU"  // Valid address
                },
                Status = WhitelistStatus.Active
            };
            var createdBy = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";

            _repositoryMock.Setup(r => r.GetEntryAsync(It.IsAny<ulong>(), It.IsAny<string>()))
                .ReturnsAsync((WhitelistEntry?)null);
            _repositoryMock.Setup(r => r.AddEntryAsync(It.IsAny<WhitelistEntry>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.BulkAddEntriesAsync(request, createdBy);

            // Assert
            if (!result.Success)
            {
                // Log the error for debugging
                Console.WriteLine($"Error: {result.ErrorMessage}");
                Console.WriteLine($"ValidationErrors: {string.Join(", ", result.ValidationErrors)}");
            }
            Assert.That(result.Success, Is.True);
            Assert.That(result.SuccessCount, Is.EqualTo(2));
            Assert.That(result.FailedCount, Is.EqualTo(0));
            Assert.That(result.SuccessfulEntries, Has.Count.EqualTo(2));
        }

        [Test]
        public async Task BulkAddEntriesAsync_DuplicateAddresses_ShouldDeduplicate()
        {
            // Arrange - Use valid Algorand testnet addresses
            var request = new BulkAddWhitelistRequest
            {
                AssetId = 12345,
                Addresses = new List<string>
                {
                    "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA",
                    "vcmjkwoy5p5p7skmzffoceropjczotijmniynuckh7lro45jmjp6uybija", // Same address, different case
                    "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA"  // Duplicate
                },
                Status = WhitelistStatus.Active
            };
            var createdBy = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";

            _repositoryMock.Setup(r => r.GetEntryAsync(It.IsAny<ulong>(), It.IsAny<string>()))
                .ReturnsAsync((WhitelistEntry?)null);
            _repositoryMock.Setup(r => r.AddEntryAsync(It.IsAny<WhitelistEntry>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.BulkAddEntriesAsync(request, createdBy);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.SuccessCount, Is.EqualTo(1)); // Only one unique address
            Assert.That(result.SuccessfulEntries, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task BulkAddEntriesAsync_MixedValidInvalid_ShouldPartiallySucceed()
        {
            // Arrange - Use valid Algorand testnet addresses
            var request = new BulkAddWhitelistRequest
            {
                AssetId = 12345,
                Addresses = new List<string>
                {
                    "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA",
                    "INVALID"
                },
                Status = WhitelistStatus.Active
            };
            var createdBy = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";

            _repositoryMock.Setup(r => r.GetEntryAsync(It.IsAny<ulong>(), It.IsAny<string>()))
                .ReturnsAsync((WhitelistEntry?)null);
            _repositoryMock.Setup(r => r.AddEntryAsync(It.IsAny<WhitelistEntry>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.BulkAddEntriesAsync(request, createdBy);

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.SuccessCount, Is.EqualTo(1));
            Assert.That(result.FailedCount, Is.EqualTo(1));
            Assert.That(result.FailedAddresses, Has.Count.EqualTo(1));
            Assert.That(result.ValidationErrors, Has.Count.EqualTo(1));
        }

        #endregion

        #region ListEntries Tests

        [Test]
        public async Task ListEntriesAsync_WithoutPagination_ShouldReturnAll()
        {
            // Arrange
            var request = new ListWhitelistRequest
            {
                AssetId = 12345,
                Page = 1,
                PageSize = 20
            };

            var entries = new List<WhitelistEntry>
            {
                new WhitelistEntry { AssetId = 12345, Address = "ADDR1", Status = WhitelistStatus.Active },
                new WhitelistEntry { AssetId = 12345, Address = "ADDR2", Status = WhitelistStatus.Active }
            };

            _repositoryMock.Setup(r => r.GetEntriesByAssetIdAsync(It.IsAny<ulong>(), It.IsAny<WhitelistStatus?>()))
                .ReturnsAsync(entries);

            // Act
            var result = await _service.ListEntriesAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Entries, Has.Count.EqualTo(2));
            Assert.That(result.TotalCount, Is.EqualTo(2));
            Assert.That(result.TotalPages, Is.EqualTo(1));
        }

        [Test]
        public async Task ListEntriesAsync_WithPagination_ShouldReturnPagedResults()
        {
            // Arrange
            var request = new ListWhitelistRequest
            {
                AssetId = 12345,
                Page = 2,
                PageSize = 2
            };

            var entries = new List<WhitelistEntry>
            {
                new WhitelistEntry { AssetId = 12345, Address = "ADDR1", Status = WhitelistStatus.Active },
                new WhitelistEntry { AssetId = 12345, Address = "ADDR2", Status = WhitelistStatus.Active },
                new WhitelistEntry { AssetId = 12345, Address = "ADDR3", Status = WhitelistStatus.Active },
                new WhitelistEntry { AssetId = 12345, Address = "ADDR4", Status = WhitelistStatus.Active }
            };

            _repositoryMock.Setup(r => r.GetEntriesByAssetIdAsync(It.IsAny<ulong>(), It.IsAny<WhitelistStatus?>()))
                .ReturnsAsync(entries);

            // Act
            var result = await _service.ListEntriesAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Entries, Has.Count.EqualTo(2));
            Assert.That(result.TotalCount, Is.EqualTo(4));
            Assert.That(result.Page, Is.EqualTo(2));
            Assert.That(result.TotalPages, Is.EqualTo(2));
        }

        [Test]
        public async Task ListEntriesAsync_WithStatusFilter_ShouldReturnFiltered()
        {
            // Arrange
            var request = new ListWhitelistRequest
            {
                AssetId = 12345,
                Status = WhitelistStatus.Active,
                Page = 1,
                PageSize = 20
            };

            var entries = new List<WhitelistEntry>
            {
                new WhitelistEntry { AssetId = 12345, Address = "ADDR1", Status = WhitelistStatus.Active },
                new WhitelistEntry { AssetId = 12345, Address = "ADDR2", Status = WhitelistStatus.Active }
            };

            _repositoryMock.Setup(r => r.GetEntriesByAssetIdAsync(It.IsAny<ulong>(), WhitelistStatus.Active))
                .ReturnsAsync(entries);

            // Act
            var result = await _service.ListEntriesAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Entries, Has.Count.EqualTo(2));
            Assert.That(result.Entries.All(e => e.Status == WhitelistStatus.Active), Is.True);
        }

        #endregion

        #region Audit Log Tests

        [Test]
        public async Task GetAuditLogAsync_ValidRequest_ShouldReturnSuccess()
        {
            // Arrange
            var auditEntries = new List<WhitelistAuditLogEntry>
            {
                new WhitelistAuditLogEntry
                {
                    AssetId = 12345,
                    Address = "ADDRESS1123456789012345678901234567890123456789012345",
                    ActionType = WhitelistActionType.Add,
                    PerformedBy = "CREATOR123456789012345678901234567890123456789012345",
                    PerformedAt = DateTime.UtcNow.AddMinutes(-10),
                    NewStatus = WhitelistStatus.Active
                },
                new WhitelistAuditLogEntry
                {
                    AssetId = 12345,
                    Address = "ADDRESS2123456789012345678901234567890123456789012345",
                    ActionType = WhitelistActionType.Update,
                    PerformedBy = "UPDATER123456789012345678901234567890123456789012345",
                    PerformedAt = DateTime.UtcNow.AddMinutes(-5),
                    OldStatus = WhitelistStatus.Active,
                    NewStatus = WhitelistStatus.Inactive
                }
            };

            _repositoryMock
                .Setup(r => r.GetAuditLogAsync(It.IsAny<GetWhitelistAuditLogRequest>()))
                .ReturnsAsync(auditEntries);

            var request = new GetWhitelistAuditLogRequest
            {
                AssetId = 12345,
                Page = 1,
                PageSize = 10
            };

            // Act
            var result = await _service.GetAuditLogAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Entries, Has.Count.EqualTo(2));
            Assert.That(result.TotalCount, Is.EqualTo(2));
            Assert.That(result.Page, Is.EqualTo(1));
            Assert.That(result.PageSize, Is.EqualTo(10));
        }

        [Test]
        public async Task GetAuditLogAsync_WithPagination_ShouldReturnCorrectPage()
        {
            // Arrange
            var auditEntries = Enumerable.Range(1, 25)
                .Select(i => new WhitelistAuditLogEntry
                {
                    AssetId = 12345,
                    Address = $"ADDRESS{i}1234567890123456789012345678901234567890123",
                    ActionType = WhitelistActionType.Add,
                    PerformedBy = "CREATOR123456789012345678901234567890123456789012345"
                })
                .ToList();

            _repositoryMock
                .Setup(r => r.GetAuditLogAsync(It.IsAny<GetWhitelistAuditLogRequest>()))
                .ReturnsAsync(auditEntries);

            var request = new GetWhitelistAuditLogRequest
            {
                AssetId = 12345,
                Page = 2,
                PageSize = 10
            };

            // Act
            var result = await _service.GetAuditLogAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Entries, Has.Count.EqualTo(10));
            Assert.That(result.TotalCount, Is.EqualTo(25));
            Assert.That(result.TotalPages, Is.EqualTo(3));
            Assert.That(result.Page, Is.EqualTo(2));
        }

        [Test]
        public async Task GetAuditLogAsync_InvalidPage_ShouldDefaultToPageOne()
        {
            // Arrange
            var auditEntries = new List<WhitelistAuditLogEntry>();

            _repositoryMock
                .Setup(r => r.GetAuditLogAsync(It.IsAny<GetWhitelistAuditLogRequest>()))
                .ReturnsAsync(auditEntries);

            var request = new GetWhitelistAuditLogRequest
            {
                AssetId = 12345,
                Page = 0, // Invalid page
                PageSize = 10
            };

            // Act
            var result = await _service.GetAuditLogAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Page, Is.EqualTo(1));
        }

        [Test]
        public async Task GetAuditLogAsync_PageSizeTooLarge_ShouldCapAt100()
        {
            // Arrange
            var auditEntries = new List<WhitelistAuditLogEntry>();

            _repositoryMock
                .Setup(r => r.GetAuditLogAsync(It.IsAny<GetWhitelistAuditLogRequest>()))
                .ReturnsAsync(auditEntries);

            var request = new GetWhitelistAuditLogRequest
            {
                AssetId = 12345,
                Page = 1,
                PageSize = 200 // Too large
            };

            // Act
            var result = await _service.GetAuditLogAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.PageSize, Is.EqualTo(100));
        }

        [Test]
        public async Task AddEntryAsync_NewEntry_ShouldRecordAuditLog()
        {
            // Arrange
            _repositoryMock
                .Setup(r => r.GetEntryAsync(It.IsAny<ulong>(), It.IsAny<string>()))
                .ReturnsAsync((WhitelistEntry?)null);

            _repositoryMock
                .Setup(r => r.AddEntryAsync(It.IsAny<WhitelistEntry>()))
                .ReturnsAsync(true);

            _repositoryMock
                .Setup(r => r.AddAuditLogEntryAsync(It.IsAny<WhitelistAuditLogEntry>()))
                .ReturnsAsync(true);

            var request = new AddWhitelistEntryRequest
            {
                AssetId = 12345,
                Address = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA",
                Status = WhitelistStatus.Active
            };

            // Act
            var result = await _service.AddEntryAsync(request, "CREATOR123456789012345678901234567890123456789012345");

            // Assert
            Assert.That(result.Success, Is.True);
            _repositoryMock.Verify(r => r.AddAuditLogEntryAsync(It.Is<WhitelistAuditLogEntry>(
                e => e.ActionType == WhitelistActionType.Add &&
                     e.AssetId == 12345 &&
                     e.NewStatus == WhitelistStatus.Active &&
                     e.OldStatus == null
            )), Times.Once);
        }

        [Test]
        public async Task AddEntryAsync_ExistingEntry_ShouldRecordUpdateAuditLog()
        {
            // Arrange
            var existingEntry = new WhitelistEntry
            {
                AssetId = 12345,
                Address = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA",
                Status = WhitelistStatus.Active,
                CreatedBy = "CREATOR123456789012345678901234567890123456789012345"
            };

            _repositoryMock
                .Setup(r => r.GetEntryAsync(It.IsAny<ulong>(), It.IsAny<string>()))
                .ReturnsAsync(existingEntry);

            _repositoryMock
                .Setup(r => r.UpdateEntryAsync(It.IsAny<WhitelistEntry>()))
                .ReturnsAsync(true);

            _repositoryMock
                .Setup(r => r.AddAuditLogEntryAsync(It.IsAny<WhitelistAuditLogEntry>()))
                .ReturnsAsync(true);

            var request = new AddWhitelistEntryRequest
            {
                AssetId = 12345,
                Address = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA",
                Status = WhitelistStatus.Inactive
            };

            // Act
            var result = await _service.AddEntryAsync(request, "UPDATER123456789012345678901234567890123456789012345");

            // Assert
            Assert.That(result.Success, Is.True);
            _repositoryMock.Verify(r => r.AddAuditLogEntryAsync(It.Is<WhitelistAuditLogEntry>(
                e => e.ActionType == WhitelistActionType.Update &&
                     e.AssetId == 12345 &&
                     e.OldStatus == WhitelistStatus.Active &&
                     e.NewStatus == WhitelistStatus.Inactive
            )), Times.Once);
        }

        [Test]
        public async Task RemoveEntryAsync_ExistingEntry_ShouldRecordRemoveAuditLog()
        {
            // Arrange
            var existingEntry = new WhitelistEntry
            {
                AssetId = 12345,
                Address = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA",
                Status = WhitelistStatus.Active,
                CreatedBy = "CREATOR123456789012345678901234567890123456789012345"
            };

            _repositoryMock
                .Setup(r => r.GetEntryAsync(It.IsAny<ulong>(), It.IsAny<string>()))
                .ReturnsAsync(existingEntry);

            _repositoryMock
                .Setup(r => r.RemoveEntryAsync(It.IsAny<ulong>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            _repositoryMock
                .Setup(r => r.AddAuditLogEntryAsync(It.IsAny<WhitelistAuditLogEntry>()))
                .ReturnsAsync(true);

            var request = new RemoveWhitelistEntryRequest
            {
                AssetId = 12345,
                Address = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA"
            };

            // Act
            var result = await _service.RemoveEntryAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            _repositoryMock.Verify(r => r.AddAuditLogEntryAsync(It.Is<WhitelistAuditLogEntry>(
                e => e.ActionType == WhitelistActionType.Remove &&
                     e.AssetId == 12345 &&
                     e.OldStatus == WhitelistStatus.Active &&
                     e.NewStatus == null
            )), Times.Once);
        }

        #endregion

        #region Metering Tests

        [Test]
        public async Task AddEntryAsync_Success_ShouldEmitMeteringEvent()
        {
            // Arrange
            var request = new AddWhitelistEntryRequest
            {
                AssetId = 12345,
                Address = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA",
                Status = WhitelistStatus.Active
            };
            var createdBy = "CREATOR1AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";

            _repositoryMock.Setup(r => r.GetEntryAsync(request.AssetId, It.IsAny<string>()))
                .ReturnsAsync((WhitelistEntry?)null);
            _repositoryMock.Setup(r => r.AddEntryAsync(It.IsAny<WhitelistEntry>()))
                .ReturnsAsync(true);
            _repositoryMock.Setup(r => r.AddAuditLogEntryAsync(It.IsAny<WhitelistAuditLogEntry>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.AddEntryAsync(request, createdBy);

            // Assert
            Assert.That(result.Success, Is.True);
            _meteringServiceMock.Verify(
                m => m.EmitMeteringEvent(It.Is<SubscriptionMeteringEvent>(
                    e => e.Category == MeteringCategory.Whitelist &&
                         e.OperationType == MeteringOperationType.Add &&
                         e.AssetId == request.AssetId &&
                         e.PerformedBy == createdBy &&
                         e.ItemCount == 1
                )),
                Times.Once);
        }

        [Test]
        public async Task AddEntryAsync_UpdateExisting_ShouldEmitUpdateMeteringEvent()
        {
            // Arrange
            var request = new AddWhitelistEntryRequest
            {
                AssetId = 12345,
                Address = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA",
                Status = WhitelistStatus.Active
            };
            var createdBy = "CREATOR1AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";

            var existingEntry = new WhitelistEntry
            {
                AssetId = request.AssetId,
                Address = request.Address.ToUpperInvariant(),
                Status = WhitelistStatus.Inactive,
                CreatedBy = "OLDUSER1AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ",
                CreatedAt = DateTime.UtcNow.AddDays(-10)
            };

            _repositoryMock.Setup(r => r.GetEntryAsync(request.AssetId, request.Address.ToUpperInvariant()))
                .ReturnsAsync(existingEntry);
            _repositoryMock.Setup(r => r.UpdateEntryAsync(It.IsAny<WhitelistEntry>()))
                .ReturnsAsync(true);
            _repositoryMock.Setup(r => r.AddAuditLogEntryAsync(It.IsAny<WhitelistAuditLogEntry>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.AddEntryAsync(request, createdBy);

            // Assert
            Assert.That(result.Success, Is.True);
            _meteringServiceMock.Verify(
                m => m.EmitMeteringEvent(It.Is<SubscriptionMeteringEvent>(
                    e => e.Category == MeteringCategory.Whitelist &&
                         e.OperationType == MeteringOperationType.Update &&
                         e.AssetId == request.AssetId &&
                         e.PerformedBy == createdBy &&
                         e.ItemCount == 1
                )),
                Times.Once);
        }

        [Test]
        public async Task RemoveEntryAsync_Success_ShouldEmitMeteringEvent()
        {
            // Arrange
            var request = new RemoveWhitelistEntryRequest
            {
                AssetId = 12345,
                Address = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA"
            };

            var existingEntry = new WhitelistEntry
            {
                AssetId = request.AssetId,
                Address = request.Address.ToUpperInvariant(),
                Status = WhitelistStatus.Active,
                CreatedBy = "CREATOR1AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ"
            };

            _repositoryMock.Setup(r => r.GetEntryAsync(request.AssetId, request.Address.ToUpperInvariant()))
                .ReturnsAsync(existingEntry);
            _repositoryMock.Setup(r => r.RemoveEntryAsync(request.AssetId, request.Address.ToUpperInvariant()))
                .ReturnsAsync(true);
            _repositoryMock.Setup(r => r.AddAuditLogEntryAsync(It.IsAny<WhitelistAuditLogEntry>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.RemoveEntryAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            _meteringServiceMock.Verify(
                m => m.EmitMeteringEvent(It.Is<SubscriptionMeteringEvent>(
                    e => e.Category == MeteringCategory.Whitelist &&
                         e.OperationType == MeteringOperationType.Remove &&
                         e.AssetId == request.AssetId &&
                         e.ItemCount == 1
                )),
                Times.Once);
        }

        [Test]
        public async Task BulkAddEntriesAsync_Success_ShouldEmitMeteringEventWithCount()
        {
            // Arrange
            var request = new BulkAddWhitelistRequest
            {
                AssetId = 12345,
                Addresses = new List<string>
                {
                    "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA",
                    "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ",
                    "7777777777777777777777777777777777777777777777777774MSJUVU"
                },
                Status = WhitelistStatus.Active
            };
            var createdBy = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";

            _repositoryMock.Setup(r => r.GetEntryAsync(It.IsAny<ulong>(), It.IsAny<string>()))
                .ReturnsAsync((WhitelistEntry?)null);
            _repositoryMock.Setup(r => r.AddEntryAsync(It.IsAny<WhitelistEntry>()))
                .ReturnsAsync(true);
            _repositoryMock.Setup(r => r.AddAuditLogEntryAsync(It.IsAny<WhitelistAuditLogEntry>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.BulkAddEntriesAsync(request, createdBy);

            // Assert
            Assert.That(result.Success, Is.True, $"Expected success but got: {result.ErrorMessage}");
            Assert.That(result.SuccessCount, Is.EqualTo(3));
            _meteringServiceMock.Verify(
                m => m.EmitMeteringEvent(It.Is<SubscriptionMeteringEvent>(
                    e => e.Category == MeteringCategory.Whitelist &&
                         e.OperationType == MeteringOperationType.BulkAdd &&
                         e.AssetId == request.AssetId &&
                         e.PerformedBy == createdBy &&
                         e.ItemCount == 3
                )),
                Times.Once);
        }

        [Test]
        public async Task BulkAddEntriesAsync_PartialSuccess_ShouldEmitMeteringEventWithSuccessCount()
        {
            // Arrange
            var request = new BulkAddWhitelistRequest
            {
                AssetId = 12345,
                Addresses = new List<string>
                {
                    "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA",
                    "INVALIDADDRESS",
                    "7777777777777777777777777777777777777777777777777774MSJUVU"
                },
                Status = WhitelistStatus.Active
            };
            var createdBy = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";

            _repositoryMock.Setup(r => r.GetEntryAsync(It.IsAny<ulong>(), It.IsAny<string>()))
                .ReturnsAsync((WhitelistEntry?)null);
            _repositoryMock.Setup(r => r.AddEntryAsync(It.IsAny<WhitelistEntry>()))
                .ReturnsAsync(true);
            _repositoryMock.Setup(r => r.AddAuditLogEntryAsync(It.IsAny<WhitelistAuditLogEntry>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.BulkAddEntriesAsync(request, createdBy);

            // Assert
            Assert.That(result.Success, Is.False); // Partial failure
            Assert.That(result.SuccessCount, Is.EqualTo(2)); // Only valid addresses
            Assert.That(result.FailedCount, Is.EqualTo(1));
            _meteringServiceMock.Verify(
                m => m.EmitMeteringEvent(It.Is<SubscriptionMeteringEvent>(
                    e => e.Category == MeteringCategory.Whitelist &&
                         e.OperationType == MeteringOperationType.BulkAdd &&
                         e.AssetId == request.AssetId &&
                         e.PerformedBy == createdBy &&
                         e.ItemCount == 2 // Only successful items
                )),
                Times.Once);
        }

        [Test]
        public async Task BulkAddEntriesAsync_AllFailed_ShouldNotEmitMeteringEvent()
        {
            // Arrange
            var request = new BulkAddWhitelistRequest
            {
                AssetId = 12345,
                Addresses = new List<string>
                {
                    "INVALIDADDRESS1",
                    "INVALIDADDRESS2"
                },
                Status = WhitelistStatus.Active
            };
            var createdBy = "ADMIN1AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";

            // Act
            var result = await _service.BulkAddEntriesAsync(request, createdBy);

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.SuccessCount, Is.EqualTo(0));
            _meteringServiceMock.Verify(
                m => m.EmitMeteringEvent(It.IsAny<SubscriptionMeteringEvent>()),
                Times.Never);
        }

        #endregion
    }
}

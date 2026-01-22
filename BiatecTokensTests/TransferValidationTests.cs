using BiatecTokensApi.Models.Metering;
using BiatecTokensApi.Models.Whitelist;
using BiatecTokensApi.Repositories;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging;
using Moq;

namespace BiatecTokensTests
{
    /// <summary>
    /// Tests for transfer validation functionality
    /// </summary>
    [TestFixture]
    public class TransferValidationTests
    {
        private Mock<IWhitelistRepository> _repositoryMock;
        private Mock<ILogger<WhitelistService>> _loggerMock;
        private Mock<ISubscriptionMeteringService> _meteringServiceMock;
        private WhitelistService _service;

        private const string ValidSenderAddress = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";
        private const string ValidReceiverAddress = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";
        private const ulong TestAssetId = 12345;

        [SetUp]
        public void Setup()
        {
            _repositoryMock = new Mock<IWhitelistRepository>();
            _loggerMock = new Mock<ILogger<WhitelistService>>();
            _meteringServiceMock = new Mock<ISubscriptionMeteringService>();
            _service = new WhitelistService(_repositoryMock.Object, _loggerMock.Object, _meteringServiceMock.Object);
        }

        #region Valid Transfer Tests

        [Test]
        public async Task ValidateTransferAsync_BothAddressesWhitelistedAndActive_ShouldAllowTransfer()
        {
            // Arrange
            var senderEntry = new WhitelistEntry
            {
                AssetId = TestAssetId,
                Address = ValidSenderAddress,
                Status = WhitelistStatus.Active,
                ExpirationDate = null
            };

            var receiverEntry = new WhitelistEntry
            {
                AssetId = TestAssetId,
                Address = ValidReceiverAddress,
                Status = WhitelistStatus.Active,
                ExpirationDate = null
            };

            _repositoryMock.Setup(r => r.GetEntryAsync(TestAssetId, ValidSenderAddress))
                .ReturnsAsync(senderEntry);
            _repositoryMock.Setup(r => r.GetEntryAsync(TestAssetId, ValidReceiverAddress))
                .ReturnsAsync(receiverEntry);

            var request = new ValidateTransferRequest
            {
                AssetId = TestAssetId,
                FromAddress = ValidSenderAddress,
                ToAddress = ValidReceiverAddress
            };

            // Act
            var result = await _service.ValidateTransferAsync(request, "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA");

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.IsAllowed, Is.True);
            Assert.That(result.DenialReason, Is.Null);
            Assert.That(result.SenderStatus, Is.Not.Null);
            Assert.That(result.SenderStatus!.IsWhitelisted, Is.True);
            Assert.That(result.SenderStatus.IsActive, Is.True);
            Assert.That(result.SenderStatus.IsExpired, Is.False);
            Assert.That(result.ReceiverStatus, Is.Not.Null);
            Assert.That(result.ReceiverStatus!.IsWhitelisted, Is.True);
            Assert.That(result.ReceiverStatus.IsActive, Is.True);
            Assert.That(result.ReceiverStatus.IsExpired, Is.False);
        }

        [Test]
        public async Task ValidateTransferAsync_BothAddressesWithFutureExpiration_ShouldAllowTransfer()
        {
            // Arrange
            var futureDate = DateTime.UtcNow.AddDays(30);
            var senderEntry = new WhitelistEntry
            {
                AssetId = TestAssetId,
                Address = ValidSenderAddress,
                Status = WhitelistStatus.Active,
                ExpirationDate = futureDate
            };

            var receiverEntry = new WhitelistEntry
            {
                AssetId = TestAssetId,
                Address = ValidReceiverAddress,
                Status = WhitelistStatus.Active,
                ExpirationDate = futureDate
            };

            _repositoryMock.Setup(r => r.GetEntryAsync(TestAssetId, ValidSenderAddress))
                .ReturnsAsync(senderEntry);
            _repositoryMock.Setup(r => r.GetEntryAsync(TestAssetId, ValidReceiverAddress))
                .ReturnsAsync(receiverEntry);

            var request = new ValidateTransferRequest
            {
                AssetId = TestAssetId,
                FromAddress = ValidSenderAddress,
                ToAddress = ValidReceiverAddress
            };

            // Act
            var result = await _service.ValidateTransferAsync(request, "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA");

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.IsAllowed, Is.True);
            Assert.That(result.DenialReason, Is.Null);
            Assert.That(result.SenderStatus!.IsExpired, Is.False);
            Assert.That(result.ReceiverStatus!.IsExpired, Is.False);
        }

        #endregion

        #region Sender Not Whitelisted Tests

        [Test]
        public async Task ValidateTransferAsync_SenderNotWhitelisted_ShouldDenyTransfer()
        {
            // Arrange
            var receiverEntry = new WhitelistEntry
            {
                AssetId = TestAssetId,
                Address = ValidReceiverAddress,
                Status = WhitelistStatus.Active
            };

            _repositoryMock.Setup(r => r.GetEntryAsync(TestAssetId, ValidSenderAddress))
                .ReturnsAsync((WhitelistEntry?)null);
            _repositoryMock.Setup(r => r.GetEntryAsync(TestAssetId, ValidReceiverAddress))
                .ReturnsAsync(receiverEntry);

            var request = new ValidateTransferRequest
            {
                AssetId = TestAssetId,
                FromAddress = ValidSenderAddress,
                ToAddress = ValidReceiverAddress
            };

            // Act
            var result = await _service.ValidateTransferAsync(request, "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA");

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.IsAllowed, Is.False);
            Assert.That(result.DenialReason, Does.Contain("Sender address"));
            Assert.That(result.DenialReason, Does.Contain("not whitelisted"));
            Assert.That(result.SenderStatus!.IsWhitelisted, Is.False);
            Assert.That(result.ReceiverStatus!.IsWhitelisted, Is.True);
        }

        [Test]
        public async Task ValidateTransferAsync_SenderInactive_ShouldDenyTransfer()
        {
            // Arrange
            var senderEntry = new WhitelistEntry
            {
                AssetId = TestAssetId,
                Address = ValidSenderAddress,
                Status = WhitelistStatus.Inactive
            };

            var receiverEntry = new WhitelistEntry
            {
                AssetId = TestAssetId,
                Address = ValidReceiverAddress,
                Status = WhitelistStatus.Active
            };

            _repositoryMock.Setup(r => r.GetEntryAsync(TestAssetId, ValidSenderAddress))
                .ReturnsAsync(senderEntry);
            _repositoryMock.Setup(r => r.GetEntryAsync(TestAssetId, ValidReceiverAddress))
                .ReturnsAsync(receiverEntry);

            var request = new ValidateTransferRequest
            {
                AssetId = TestAssetId,
                FromAddress = ValidSenderAddress,
                ToAddress = ValidReceiverAddress
            };

            // Act
            var result = await _service.ValidateTransferAsync(request, "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA");

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.IsAllowed, Is.False);
            Assert.That(result.DenialReason, Does.Contain("Sender address"));
            Assert.That(result.DenialReason, Does.Contain("Inactive"));
            Assert.That(result.SenderStatus!.IsActive, Is.False);
            Assert.That(result.SenderStatus.Status, Is.EqualTo(WhitelistStatus.Inactive));
        }

        [Test]
        public async Task ValidateTransferAsync_SenderRevoked_ShouldDenyTransfer()
        {
            // Arrange
            var senderEntry = new WhitelistEntry
            {
                AssetId = TestAssetId,
                Address = ValidSenderAddress,
                Status = WhitelistStatus.Revoked
            };

            var receiverEntry = new WhitelistEntry
            {
                AssetId = TestAssetId,
                Address = ValidReceiverAddress,
                Status = WhitelistStatus.Active
            };

            _repositoryMock.Setup(r => r.GetEntryAsync(TestAssetId, ValidSenderAddress))
                .ReturnsAsync(senderEntry);
            _repositoryMock.Setup(r => r.GetEntryAsync(TestAssetId, ValidReceiverAddress))
                .ReturnsAsync(receiverEntry);

            var request = new ValidateTransferRequest
            {
                AssetId = TestAssetId,
                FromAddress = ValidSenderAddress,
                ToAddress = ValidReceiverAddress
            };

            // Act
            var result = await _service.ValidateTransferAsync(request, "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA");

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.IsAllowed, Is.False);
            Assert.That(result.DenialReason, Does.Contain("Sender address"));
            Assert.That(result.DenialReason, Does.Contain("Revoked"));
            Assert.That(result.SenderStatus!.Status, Is.EqualTo(WhitelistStatus.Revoked));
        }

        [Test]
        public async Task ValidateTransferAsync_SenderExpired_ShouldDenyTransfer()
        {
            // Arrange
            var pastDate = DateTime.UtcNow.AddDays(-1);
            var senderEntry = new WhitelistEntry
            {
                AssetId = TestAssetId,
                Address = ValidSenderAddress,
                Status = WhitelistStatus.Active,
                ExpirationDate = pastDate
            };

            var receiverEntry = new WhitelistEntry
            {
                AssetId = TestAssetId,
                Address = ValidReceiverAddress,
                Status = WhitelistStatus.Active
            };

            _repositoryMock.Setup(r => r.GetEntryAsync(TestAssetId, ValidSenderAddress))
                .ReturnsAsync(senderEntry);
            _repositoryMock.Setup(r => r.GetEntryAsync(TestAssetId, ValidReceiverAddress))
                .ReturnsAsync(receiverEntry);

            var request = new ValidateTransferRequest
            {
                AssetId = TestAssetId,
                FromAddress = ValidSenderAddress,
                ToAddress = ValidReceiverAddress
            };

            // Act
            var result = await _service.ValidateTransferAsync(request, "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA");

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.IsAllowed, Is.False);
            Assert.That(result.DenialReason, Does.Contain("Sender address"));
            Assert.That(result.DenialReason, Does.Contain("expired"));
            Assert.That(result.SenderStatus!.IsExpired, Is.True);
            Assert.That(result.SenderStatus.ExpirationDate, Is.EqualTo(pastDate));
        }

        #endregion

        #region Receiver Not Whitelisted Tests

        [Test]
        public async Task ValidateTransferAsync_ReceiverNotWhitelisted_ShouldDenyTransfer()
        {
            // Arrange
            var senderEntry = new WhitelistEntry
            {
                AssetId = TestAssetId,
                Address = ValidSenderAddress,
                Status = WhitelistStatus.Active
            };

            _repositoryMock.Setup(r => r.GetEntryAsync(TestAssetId, ValidSenderAddress))
                .ReturnsAsync(senderEntry);
            _repositoryMock.Setup(r => r.GetEntryAsync(TestAssetId, ValidReceiverAddress))
                .ReturnsAsync((WhitelistEntry?)null);

            var request = new ValidateTransferRequest
            {
                AssetId = TestAssetId,
                FromAddress = ValidSenderAddress,
                ToAddress = ValidReceiverAddress
            };

            // Act
            var result = await _service.ValidateTransferAsync(request, "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA");

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.IsAllowed, Is.False);
            Assert.That(result.DenialReason, Does.Contain("Receiver address"));
            Assert.That(result.DenialReason, Does.Contain("not whitelisted"));
            Assert.That(result.SenderStatus!.IsWhitelisted, Is.True);
            Assert.That(result.ReceiverStatus!.IsWhitelisted, Is.False);
        }

        [Test]
        public async Task ValidateTransferAsync_ReceiverInactive_ShouldDenyTransfer()
        {
            // Arrange
            var senderEntry = new WhitelistEntry
            {
                AssetId = TestAssetId,
                Address = ValidSenderAddress,
                Status = WhitelistStatus.Active
            };

            var receiverEntry = new WhitelistEntry
            {
                AssetId = TestAssetId,
                Address = ValidReceiverAddress,
                Status = WhitelistStatus.Inactive
            };

            _repositoryMock.Setup(r => r.GetEntryAsync(TestAssetId, ValidSenderAddress))
                .ReturnsAsync(senderEntry);
            _repositoryMock.Setup(r => r.GetEntryAsync(TestAssetId, ValidReceiverAddress))
                .ReturnsAsync(receiverEntry);

            var request = new ValidateTransferRequest
            {
                AssetId = TestAssetId,
                FromAddress = ValidSenderAddress,
                ToAddress = ValidReceiverAddress
            };

            // Act
            var result = await _service.ValidateTransferAsync(request, "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA");

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.IsAllowed, Is.False);
            Assert.That(result.DenialReason, Does.Contain("Receiver address"));
            Assert.That(result.DenialReason, Does.Contain("Inactive"));
            Assert.That(result.ReceiverStatus!.IsActive, Is.False);
        }

        [Test]
        public async Task ValidateTransferAsync_ReceiverExpired_ShouldDenyTransfer()
        {
            // Arrange
            var pastDate = DateTime.UtcNow.AddDays(-5);
            var senderEntry = new WhitelistEntry
            {
                AssetId = TestAssetId,
                Address = ValidSenderAddress,
                Status = WhitelistStatus.Active
            };

            var receiverEntry = new WhitelistEntry
            {
                AssetId = TestAssetId,
                Address = ValidReceiverAddress,
                Status = WhitelistStatus.Active,
                ExpirationDate = pastDate
            };

            _repositoryMock.Setup(r => r.GetEntryAsync(TestAssetId, ValidSenderAddress))
                .ReturnsAsync(senderEntry);
            _repositoryMock.Setup(r => r.GetEntryAsync(TestAssetId, ValidReceiverAddress))
                .ReturnsAsync(receiverEntry);

            var request = new ValidateTransferRequest
            {
                AssetId = TestAssetId,
                FromAddress = ValidSenderAddress,
                ToAddress = ValidReceiverAddress
            };

            // Act
            var result = await _service.ValidateTransferAsync(request, "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA");

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.IsAllowed, Is.False);
            Assert.That(result.DenialReason, Does.Contain("Receiver address"));
            Assert.That(result.DenialReason, Does.Contain("expired"));
            Assert.That(result.ReceiverStatus!.IsExpired, Is.True);
        }

        #endregion

        #region Both Addresses Issues Tests

        [Test]
        public async Task ValidateTransferAsync_BothAddressesNotWhitelisted_ShouldDenyWithBothReasons()
        {
            // Arrange
            _repositoryMock.Setup(r => r.GetEntryAsync(TestAssetId, ValidSenderAddress))
                .ReturnsAsync((WhitelistEntry?)null);
            _repositoryMock.Setup(r => r.GetEntryAsync(TestAssetId, ValidReceiverAddress))
                .ReturnsAsync((WhitelistEntry?)null);

            var request = new ValidateTransferRequest
            {
                AssetId = TestAssetId,
                FromAddress = ValidSenderAddress,
                ToAddress = ValidReceiverAddress
            };

            // Act
            var result = await _service.ValidateTransferAsync(request, "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA");

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.IsAllowed, Is.False);
            Assert.That(result.DenialReason, Does.Contain("Sender address"));
            Assert.That(result.DenialReason, Does.Contain("Receiver address"));
            Assert.That(result.SenderStatus!.IsWhitelisted, Is.False);
            Assert.That(result.ReceiverStatus!.IsWhitelisted, Is.False);
        }

        [Test]
        public async Task ValidateTransferAsync_BothAddressesExpired_ShouldDenyWithBothReasons()
        {
            // Arrange
            var pastDate = DateTime.UtcNow.AddDays(-10);
            var senderEntry = new WhitelistEntry
            {
                AssetId = TestAssetId,
                Address = ValidSenderAddress,
                Status = WhitelistStatus.Active,
                ExpirationDate = pastDate
            };

            var receiverEntry = new WhitelistEntry
            {
                AssetId = TestAssetId,
                Address = ValidReceiverAddress,
                Status = WhitelistStatus.Active,
                ExpirationDate = pastDate
            };

            _repositoryMock.Setup(r => r.GetEntryAsync(TestAssetId, ValidSenderAddress))
                .ReturnsAsync(senderEntry);
            _repositoryMock.Setup(r => r.GetEntryAsync(TestAssetId, ValidReceiverAddress))
                .ReturnsAsync(receiverEntry);

            var request = new ValidateTransferRequest
            {
                AssetId = TestAssetId,
                FromAddress = ValidSenderAddress,
                ToAddress = ValidReceiverAddress
            };

            // Act
            var result = await _service.ValidateTransferAsync(request, "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA");

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.IsAllowed, Is.False);
            Assert.That(result.DenialReason, Does.Contain("Sender address"));
            Assert.That(result.DenialReason, Does.Contain("Receiver address"));
            Assert.That(result.DenialReason, Does.Contain("expired"));
            Assert.That(result.SenderStatus!.IsExpired, Is.True);
            Assert.That(result.ReceiverStatus!.IsExpired, Is.True);
        }

        #endregion

        #region Invalid Address Tests

        [Test]
        public async Task ValidateTransferAsync_InvalidSenderAddress_ShouldReturnError()
        {
            // Arrange
            var request = new ValidateTransferRequest
            {
                AssetId = TestAssetId,
                FromAddress = "INVALID_ADDRESS",
                ToAddress = ValidReceiverAddress
            };

            // Act
            var result = await _service.ValidateTransferAsync(request, "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA");

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.IsAllowed, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("Invalid sender address format"));
            Assert.That(result.DenialReason, Does.Contain("Invalid sender address format"));
        }

        [Test]
        public async Task ValidateTransferAsync_InvalidReceiverAddress_ShouldReturnError()
        {
            // Arrange
            var request = new ValidateTransferRequest
            {
                AssetId = TestAssetId,
                FromAddress = ValidSenderAddress,
                ToAddress = "SHORT"
            };

            // Act
            var result = await _service.ValidateTransferAsync(request, "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA");

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.IsAllowed, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("Invalid receiver address format"));
            Assert.That(result.DenialReason, Does.Contain("Invalid receiver address format"));
        }

        [Test]
        public async Task ValidateTransferAsync_EmptySenderAddress_ShouldReturnError()
        {
            // Arrange
            var request = new ValidateTransferRequest
            {
                AssetId = TestAssetId,
                FromAddress = "",
                ToAddress = ValidReceiverAddress
            };

            // Act
            var result = await _service.ValidateTransferAsync(request, "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA");

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.IsAllowed, Is.False);
            Assert.That(result.DenialReason, Does.Contain("Invalid sender address format"));
        }

        #endregion
    }
}

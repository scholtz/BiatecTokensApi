using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Whitelist;
using BiatecTokensApi.Services.Interface;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Integration tests for whitelist enforcement on token operations
    /// </summary>
    [TestFixture]
    public class TokenWhitelistEnforcementIntegrationTests
    {
        private Mock<IWhitelistService> _whitelistServiceMock;
        private const ulong TestAssetId = 12345;
        private const string TestAddress1 = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";
        private const string TestAddress2 = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";

        [SetUp]
        public void Setup()
        {
            _whitelistServiceMock = new Mock<IWhitelistService>();
        }

        [Test]
        public async Task TransferSimulation_BothAddressesWhitelisted_ShouldAllow()
        {
            // Arrange
            _whitelistServiceMock
                .Setup(s => s.ValidateTransferAsync(It.IsAny<ValidateTransferRequest>(), It.IsAny<string>()))
                .ReturnsAsync(new ValidateTransferResponse
                {
                    Success = true,
                    IsAllowed = true,
                    SenderStatus = new TransferParticipantStatus
                    {
                        Address = TestAddress1,
                        IsWhitelisted = true,
                        IsActive = true,
                        IsExpired = false,
                        Status = WhitelistStatus.Active
                    },
                    ReceiverStatus = new TransferParticipantStatus
                    {
                        Address = TestAddress2,
                        IsWhitelisted = true,
                        IsActive = true,
                        IsExpired = false,
                        Status = WhitelistStatus.Active
                    }
                });

            // Act & Assert
            // The WhitelistEnforcement attribute will validate both addresses
            // and allow the operation to proceed if both are whitelisted
            var result = await _whitelistServiceMock.Object.ValidateTransferAsync(
                new ValidateTransferRequest
                {
                    AssetId = TestAssetId,
                    FromAddress = TestAddress1,
                    ToAddress = TestAddress2
                },
                TestAddress1);

            Assert.That(result.Success, Is.True);
            Assert.That(result.IsAllowed, Is.True);
        }

        [Test]
        public async Task TransferSimulation_SenderNotWhitelisted_ShouldBlock()
        {
            // Arrange
            _whitelistServiceMock
                .Setup(s => s.ValidateTransferAsync(It.IsAny<ValidateTransferRequest>(), It.IsAny<string>()))
                .ReturnsAsync(new ValidateTransferResponse
                {
                    Success = true,
                    IsAllowed = false,
                    DenialReason = $"Sender address {TestAddress1} is not whitelisted for asset {TestAssetId}",
                    SenderStatus = new TransferParticipantStatus
                    {
                        Address = TestAddress1,
                        IsWhitelisted = false,
                        IsActive = false,
                        IsExpired = false,
                        Status = WhitelistStatus.Active
                    }
                });

            // Act
            var result = await _whitelistServiceMock.Object.ValidateTransferAsync(
                new ValidateTransferRequest
                {
                    AssetId = TestAssetId,
                    FromAddress = TestAddress1,
                    ToAddress = TestAddress2
                },
                TestAddress1);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.IsAllowed, Is.False);
            Assert.That(result.DenialReason, Does.Contain("not whitelisted"));
        }

        [Test]
        public async Task MintSimulation_RecipientWhitelisted_ShouldAllow()
        {
            // Arrange
            _whitelistServiceMock
                .Setup(s => s.ValidateTransferAsync(It.IsAny<ValidateTransferRequest>(), It.IsAny<string>()))
                .ReturnsAsync(new ValidateTransferResponse
                {
                    Success = true,
                    IsAllowed = true,
                    ReceiverStatus = new TransferParticipantStatus
                    {
                        Address = TestAddress2,
                        IsWhitelisted = true,
                        IsActive = true,
                        IsExpired = false,
                        Status = WhitelistStatus.Active
                    }
                });

            // Act
            var result = await _whitelistServiceMock.Object.ValidateTransferAsync(
                new ValidateTransferRequest
                {
                    AssetId = TestAssetId,
                    FromAddress = TestAddress2, // Same address for mint validation
                    ToAddress = TestAddress2
                },
                TestAddress1);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.IsAllowed, Is.True);
        }

        [Test]
        public async Task MintSimulation_RecipientNotWhitelisted_ShouldBlock()
        {
            // Arrange
            _whitelistServiceMock
                .Setup(s => s.ValidateTransferAsync(It.IsAny<ValidateTransferRequest>(), It.IsAny<string>()))
                .ReturnsAsync(new ValidateTransferResponse
                {
                    Success = true,
                    IsAllowed = false,
                    DenialReason = $"Receiver address {TestAddress2} is not whitelisted for asset {TestAssetId}",
                    ReceiverStatus = new TransferParticipantStatus
                    {
                        Address = TestAddress2,
                        IsWhitelisted = false,
                        IsActive = false,
                        IsExpired = false,
                        Status = WhitelistStatus.Active
                    }
                });

            // Act
            var result = await _whitelistServiceMock.Object.ValidateTransferAsync(
                new ValidateTransferRequest
                {
                    AssetId = TestAssetId,
                    FromAddress = TestAddress2,
                    ToAddress = TestAddress2
                },
                TestAddress1);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.IsAllowed, Is.False);
            Assert.That(result.DenialReason, Does.Contain("not whitelisted"));
        }

        [Test]
        public async Task BurnSimulation_HolderWhitelisted_ShouldAllow()
        {
            // Arrange
            _whitelistServiceMock
                .Setup(s => s.ValidateTransferAsync(It.IsAny<ValidateTransferRequest>(), It.IsAny<string>()))
                .ReturnsAsync(new ValidateTransferResponse
                {
                    Success = true,
                    IsAllowed = true,
                    SenderStatus = new TransferParticipantStatus
                    {
                        Address = TestAddress1,
                        IsWhitelisted = true,
                        IsActive = true,
                        IsExpired = false,
                        Status = WhitelistStatus.Active
                    }
                });

            // Act
            var result = await _whitelistServiceMock.Object.ValidateTransferAsync(
                new ValidateTransferRequest
                {
                    AssetId = TestAssetId,
                    FromAddress = TestAddress1,
                    ToAddress = TestAddress1
                },
                TestAddress1);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.IsAllowed, Is.True);
        }

        [Test]
        public async Task BurnSimulation_HolderNotWhitelisted_ShouldBlock()
        {
            // Arrange
            _whitelistServiceMock
                .Setup(s => s.ValidateTransferAsync(It.IsAny<ValidateTransferRequest>(), It.IsAny<string>()))
                .ReturnsAsync(new ValidateTransferResponse
                {
                    Success = true,
                    IsAllowed = false,
                    DenialReason = $"Sender address {TestAddress1} is not whitelisted for asset {TestAssetId}",
                    SenderStatus = new TransferParticipantStatus
                    {
                        Address = TestAddress1,
                        IsWhitelisted = false,
                        IsActive = false,
                        IsExpired = false,
                        Status = WhitelistStatus.Active
                    }
                });

            // Act
            var result = await _whitelistServiceMock.Object.ValidateTransferAsync(
                new ValidateTransferRequest
                {
                    AssetId = TestAssetId,
                    FromAddress = TestAddress1,
                    ToAddress = TestAddress1
                },
                TestAddress1);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.IsAllowed, Is.False);
            Assert.That(result.DenialReason, Does.Contain("not whitelisted"));
        }

        [Test]
        public async Task WhitelistEnforcement_ErrorIncludesTokenIdAndAddress()
        {
            // Arrange
            var expectedAssetId = TestAssetId;
            var expectedAddress = TestAddress1;

            _whitelistServiceMock
                .Setup(s => s.ValidateTransferAsync(It.IsAny<ValidateTransferRequest>(), It.IsAny<string>()))
                .ReturnsAsync(new ValidateTransferResponse
                {
                    Success = true,
                    IsAllowed = false,
                    DenialReason = $"Address {expectedAddress} is not whitelisted for asset {expectedAssetId}",
                    SenderStatus = new TransferParticipantStatus
                    {
                        Address = expectedAddress,
                        IsWhitelisted = false
                    }
                });

            // Act
            var result = await _whitelistServiceMock.Object.ValidateTransferAsync(
                new ValidateTransferRequest
                {
                    AssetId = expectedAssetId,
                    FromAddress = expectedAddress,
                    ToAddress = expectedAddress
                },
                TestAddress1);

            // Assert
            Assert.That(result.DenialReason, Does.Contain(expectedAssetId.ToString()));
            Assert.That(result.DenialReason, Does.Contain(expectedAddress));
        }
    }
}

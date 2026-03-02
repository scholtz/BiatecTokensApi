using BiatecTokensApi.Controllers;
using BiatecTokensApi.Models.Wallet;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Unit tests for WalletController.
    /// Covers all endpoints: GetConnectionState, Connect, Disconnect, Reconnect,
    /// GetReconnectGuidance, GetSupportedNetworks, and ValidateAddress.
    /// Includes happy path, validation error, and service-exception branches.
    /// </summary>
    [TestFixture]
    public class WalletControllerTests
    {
        private Mock<IWalletConnectionService> _serviceMock = null!;
        private Mock<ILogger<WalletController>> _loggerMock = null!;
        private WalletController _controller = null!;

        private const string AlgorandAddress = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
        private const string EvmAddress = "0xAbCdEf1234567890abcdef1234567890AbCdEf12";
        private const string MainNet = "algorand-mainnet";
        private const string TestNet = "algorand-testnet";

        [SetUp]
        public void SetUp()
        {
            _serviceMock = new Mock<IWalletConnectionService>();
            _loggerMock = new Mock<ILogger<WalletController>>();
            _controller = new WalletController(_serviceMock.Object, _loggerMock.Object);
        }

        // ── GetConnectionState ────────────────────────────────────────────────

        [Test]
        public void GetConnectionState_ValidAddress_ReturnsOkWithState()
        {
            // Arrange
            var expectedState = new WalletConnectionState
            {
                WalletAddress = AlgorandAddress,
                Status = WalletConnectionStatus.Disconnected,
                ExpectedNetwork = MainNet
            };
            _serviceMock.Setup(s => s.GetConnectionState(AlgorandAddress, MainNet))
                .Returns(expectedState);

            // Act
            var result = _controller.GetConnectionState(AlgorandAddress, MainNet) as OkObjectResult;

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.StatusCode, Is.EqualTo(200));
            var state = result.Value as WalletConnectionState;
            Assert.That(state, Is.Not.Null);
            Assert.That(state!.WalletAddress, Is.EqualTo(AlgorandAddress));
        }

        [Test]
        public void GetConnectionState_EmptyAddress_ReturnsBadRequest()
        {
            // Act
            var result = _controller.GetConnectionState("") as BadRequestObjectResult;

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.StatusCode, Is.EqualTo(400));
            _serviceMock.Verify(s => s.GetConnectionState(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Test]
        public void GetConnectionState_NullAddress_ReturnsBadRequest()
        {
            // Act
            var result = _controller.GetConnectionState(null!) as BadRequestObjectResult;

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.StatusCode, Is.EqualTo(400));
        }

        [Test]
        public void GetConnectionState_ServiceThrows_Returns500()
        {
            // Arrange
            _serviceMock.Setup(s => s.GetConnectionState(It.IsAny<string>(), It.IsAny<string>()))
                .Throws(new InvalidOperationException("Service failure"));

            // Act
            var result = _controller.GetConnectionState(AlgorandAddress, MainNet) as ObjectResult;

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.StatusCode, Is.EqualTo(500));
        }

        [Test]
        public void GetConnectionState_DefaultsToAlgorandMainnet_WhenNetworkNotProvided()
        {
            // Arrange
            var expectedState = new WalletConnectionState
            {
                WalletAddress = AlgorandAddress,
                Status = WalletConnectionStatus.Disconnected
            };
            _serviceMock.Setup(s => s.GetConnectionState(AlgorandAddress, "algorand-mainnet"))
                .Returns(expectedState);

            // Act - use default network parameter
            var result = _controller.GetConnectionState(AlgorandAddress) as OkObjectResult;

            // Assert
            Assert.That(result, Is.Not.Null);
            _serviceMock.Verify(s => s.GetConnectionState(AlgorandAddress, "algorand-mainnet"), Times.Once);
        }

        // ── Connect ───────────────────────────────────────────────────────────

        [Test]
        public void Connect_ValidRequest_ReturnsOkWithState()
        {
            // Arrange
            var connectedState = new WalletConnectionState
            {
                WalletAddress = AlgorandAddress,
                Status = WalletConnectionStatus.Connected,
                ActualNetwork = MainNet,
                ExpectedNetwork = MainNet
            };
            _serviceMock.Setup(s => s.Connect(AlgorandAddress, MainNet, MainNet))
                .Returns(connectedState);
            var request = new WalletConnectRequest
            {
                Address = AlgorandAddress,
                ActualNetwork = MainNet,
                ExpectedNetwork = MainNet
            };

            // Act
            var result = _controller.Connect(request) as OkObjectResult;

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.StatusCode, Is.EqualTo(200));
            var state = result.Value as WalletConnectionState;
            Assert.That(state!.Status, Is.EqualTo(WalletConnectionStatus.Connected));
        }

        [Test]
        public void Connect_EmptyAddress_ReturnsBadRequest()
        {
            // Arrange
            var request = new WalletConnectRequest { Address = "", ActualNetwork = MainNet };

            // Act
            var result = _controller.Connect(request) as BadRequestObjectResult;

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.StatusCode, Is.EqualTo(400));
        }

        [Test]
        public void Connect_EmptyActualNetwork_ReturnsBadRequest()
        {
            // Arrange
            var request = new WalletConnectRequest { Address = AlgorandAddress, ActualNetwork = "" };

            // Act
            var result = _controller.Connect(request) as BadRequestObjectResult;

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.StatusCode, Is.EqualTo(400));
        }

        [Test]
        public void Connect_WithNetworkMismatch_ReturnsNetworkMismatchState()
        {
            // Arrange
            var mismatchState = new WalletConnectionState
            {
                WalletAddress = AlgorandAddress,
                Status = WalletConnectionStatus.NetworkMismatch,
                ActualNetwork = TestNet,
                ExpectedNetwork = MainNet,
                NetworkMismatch = new NetworkMismatchInfo
                {
                    ActualNetwork = TestNet,
                    ExpectedNetwork = MainNet,
                    Description = "Connected to testnet but mainnet required"
                }
            };
            _serviceMock.Setup(s => s.Connect(AlgorandAddress, TestNet, MainNet))
                .Returns(mismatchState);
            var request = new WalletConnectRequest
            {
                Address = AlgorandAddress,
                ActualNetwork = TestNet,
                ExpectedNetwork = MainNet
            };

            // Act
            var result = _controller.Connect(request) as OkObjectResult;

            // Assert
            Assert.That(result, Is.Not.Null);
            var state = result!.Value as WalletConnectionState;
            Assert.That(state!.Status, Is.EqualTo(WalletConnectionStatus.NetworkMismatch));
            Assert.That(state.NetworkMismatch, Is.Not.Null);
        }

        [Test]
        public void Connect_WhenExpectedNetworkOmitted_DefaultsToActualNetwork()
        {
            // Arrange
            var connectedState = new WalletConnectionState
            {
                WalletAddress = AlgorandAddress,
                Status = WalletConnectionStatus.Connected,
                ActualNetwork = MainNet,
                ExpectedNetwork = MainNet
            };
            _serviceMock.Setup(s => s.Connect(AlgorandAddress, MainNet, MainNet))
                .Returns(connectedState);
            var request = new WalletConnectRequest
            {
                Address = AlgorandAddress,
                ActualNetwork = MainNet
                // ExpectedNetwork omitted
            };

            // Act
            _controller.Connect(request);

            // Assert - should use ActualNetwork as ExpectedNetwork
            _serviceMock.Verify(s => s.Connect(AlgorandAddress, MainNet, MainNet), Times.Once);
        }

        [Test]
        public void Connect_ServiceThrows_Returns500()
        {
            // Arrange
            _serviceMock.Setup(s => s.Connect(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Throws(new Exception("Service error"));
            var request = new WalletConnectRequest
            {
                Address = AlgorandAddress,
                ActualNetwork = MainNet
            };

            // Act
            var result = _controller.Connect(request) as ObjectResult;

            // Assert
            Assert.That(result!.StatusCode, Is.EqualTo(500));
        }

        // ── Disconnect ────────────────────────────────────────────────────────

        [Test]
        public void Disconnect_ValidAddress_ReturnsOkWithDisconnectedState()
        {
            // Arrange
            var disconnectedState = new WalletConnectionState
            {
                WalletAddress = AlgorandAddress,
                Status = WalletConnectionStatus.Disconnected
            };
            _serviceMock.Setup(s => s.Disconnect(AlgorandAddress))
                .Returns(disconnectedState);
            var request = new WalletDisconnectRequest { Address = AlgorandAddress };

            // Act
            var result = _controller.Disconnect(request) as OkObjectResult;

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.StatusCode, Is.EqualTo(200));
            var state = result.Value as WalletConnectionState;
            Assert.That(state!.Status, Is.EqualTo(WalletConnectionStatus.Disconnected));
        }

        [Test]
        public void Disconnect_EmptyAddress_ReturnsBadRequest()
        {
            // Arrange
            var request = new WalletDisconnectRequest { Address = "" };

            // Act
            var result = _controller.Disconnect(request) as BadRequestObjectResult;

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.StatusCode, Is.EqualTo(400));
            _serviceMock.Verify(s => s.Disconnect(It.IsAny<string>()), Times.Never);
        }

        [Test]
        public void Disconnect_ServiceThrows_Returns500()
        {
            // Arrange
            _serviceMock.Setup(s => s.Disconnect(It.IsAny<string>()))
                .Throws(new InvalidOperationException("Service error"));
            var request = new WalletDisconnectRequest { Address = AlgorandAddress };

            // Act
            var result = _controller.Disconnect(request) as ObjectResult;

            // Assert
            Assert.That(result!.StatusCode, Is.EqualTo(500));
        }

        // ── Reconnect ─────────────────────────────────────────────────────────

        [Test]
        public void Reconnect_ValidRequest_ReturnsOkWithState()
        {
            // Arrange
            var reconnectedState = new WalletConnectionState
            {
                WalletAddress = AlgorandAddress,
                Status = WalletConnectionStatus.Connected,
                ActualNetwork = MainNet,
                ExpectedNetwork = MainNet
            };
            _serviceMock.Setup(s => s.Reconnect(AlgorandAddress, MainNet, MainNet))
                .Returns(reconnectedState);
            var request = new WalletReconnectRequest
            {
                Address = AlgorandAddress,
                ActualNetwork = MainNet,
                ExpectedNetwork = MainNet
            };

            // Act
            var result = _controller.Reconnect(request) as OkObjectResult;

            // Assert
            Assert.That(result, Is.Not.Null);
            var state = result!.Value as WalletConnectionState;
            Assert.That(state!.Status, Is.EqualTo(WalletConnectionStatus.Connected));
        }

        [Test]
        public void Reconnect_EmptyAddress_ReturnsBadRequest()
        {
            // Arrange
            var request = new WalletReconnectRequest { Address = "", ActualNetwork = MainNet };

            // Act
            var result = _controller.Reconnect(request) as BadRequestObjectResult;

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.StatusCode, Is.EqualTo(400));
        }

        [Test]
        public void Reconnect_EmptyActualNetwork_ReturnsBadRequest()
        {
            // Arrange
            var request = new WalletReconnectRequest { Address = AlgorandAddress, ActualNetwork = "" };

            // Act
            var result = _controller.Reconnect(request) as BadRequestObjectResult;

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.StatusCode, Is.EqualTo(400));
        }

        [Test]
        public void Reconnect_WhenExpectedNetworkOmitted_DefaultsToActualNetwork()
        {
            // Arrange
            var state = new WalletConnectionState { WalletAddress = AlgorandAddress, Status = WalletConnectionStatus.Connected };
            _serviceMock.Setup(s => s.Reconnect(AlgorandAddress, MainNet, MainNet)).Returns(state);
            var request = new WalletReconnectRequest { Address = AlgorandAddress, ActualNetwork = MainNet };

            // Act
            _controller.Reconnect(request);

            // Assert
            _serviceMock.Verify(s => s.Reconnect(AlgorandAddress, MainNet, MainNet), Times.Once);
        }

        [Test]
        public void Reconnect_ServiceThrows_Returns500()
        {
            // Arrange
            _serviceMock.Setup(s => s.Reconnect(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Throws(new Exception("Service error"));
            var request = new WalletReconnectRequest { Address = AlgorandAddress, ActualNetwork = MainNet };

            // Act
            var result = _controller.Reconnect(request) as ObjectResult;

            // Assert
            Assert.That(result!.StatusCode, Is.EqualTo(500));
        }

        // ── GetReconnectGuidance ──────────────────────────────────────────────

        [Test]
        public void GetReconnectGuidance_SessionExpired_ReturnsGuidanceWithSteps()
        {
            // Arrange
            var guidance = new WalletReconnectGuidance
            {
                Reason = WalletReconnectReason.SessionExpired,
                Explanation = "Your session has expired.",
                Steps = new List<WalletReconnectStep>
                {
                    new WalletReconnectStep { StepNumber = 1, Title = "Re-authenticate", Instruction = "Click sign in again." }
                }
            };
            _serviceMock.Setup(s => s.GetReconnectGuidance(WalletReconnectReason.SessionExpired, null, null))
                .Returns(guidance);

            // Act
            var result = _controller.GetReconnectGuidance(WalletReconnectReason.SessionExpired) as OkObjectResult;

            // Assert
            Assert.That(result, Is.Not.Null);
            var returned = result!.Value as WalletReconnectGuidance;
            Assert.That(returned!.Reason, Is.EqualTo(WalletReconnectReason.SessionExpired));
            Assert.That(returned.Steps.Count, Is.GreaterThan(0));
        }

        [Test]
        public void GetReconnectGuidance_NetworkMismatch_ReturnsNetworkSwitchGuidance()
        {
            // Arrange
            var guidance = new WalletReconnectGuidance
            {
                Reason = WalletReconnectReason.NetworkMismatch,
                Explanation = "Wrong network connected.",
                Steps = new List<WalletReconnectStep>
                {
                    new WalletReconnectStep { StepNumber = 1, Title = "Switch network", Instruction = "Switch to mainnet in your wallet." }
                }
            };
            _serviceMock.Setup(s => s.GetReconnectGuidance(WalletReconnectReason.NetworkMismatch, TestNet, MainNet))
                .Returns(guidance);

            // Act
            var result = _controller.GetReconnectGuidance(WalletReconnectReason.NetworkMismatch, TestNet, MainNet) as OkObjectResult;

            // Assert
            Assert.That(result, Is.Not.Null);
            var returned = result!.Value as WalletReconnectGuidance;
            Assert.That(returned!.Reason, Is.EqualTo(WalletReconnectReason.NetworkMismatch));
        }

        [Test]
        public void GetReconnectGuidance_DefaultReason_ReturnsUnknownGuidance()
        {
            // Arrange
            var guidance = new WalletReconnectGuidance { Reason = WalletReconnectReason.Unknown };
            _serviceMock.Setup(s => s.GetReconnectGuidance(WalletReconnectReason.Unknown, null, null))
                .Returns(guidance);

            // Act - use default parameter value
            var result = _controller.GetReconnectGuidance() as OkObjectResult;

            // Assert
            Assert.That(result, Is.Not.Null);
        }

        [Test]
        public void GetReconnectGuidance_ServiceThrows_Returns500()
        {
            // Arrange
            _serviceMock.Setup(s => s.GetReconnectGuidance(It.IsAny<WalletReconnectReason>(), It.IsAny<string>(), It.IsAny<string>()))
                .Throws(new Exception("Service error"));

            // Act
            var result = _controller.GetReconnectGuidance(WalletReconnectReason.Unknown) as ObjectResult;

            // Assert
            Assert.That(result!.StatusCode, Is.EqualTo(500));
        }

        // ── GetSupportedNetworks ──────────────────────────────────────────────

        [Test]
        public void GetSupportedNetworks_ReturnsOkWithNetworkList()
        {
            // Arrange
            var networks = new List<string>
            {
                "algorand-mainnet", "algorand-testnet", "base-mainnet", "ethereum-mainnet"
            };
            _serviceMock.Setup(s => s.GetSupportedNetworks()).Returns(networks.AsReadOnly());

            // Act
            var result = _controller.GetSupportedNetworks() as OkObjectResult;

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.StatusCode, Is.EqualTo(200));
            var response = result.Value as SupportedNetworksResponse;
            Assert.That(response, Is.Not.Null);
            Assert.That(response!.Networks.Count, Is.EqualTo(4));
            Assert.That(response.TotalCount, Is.EqualTo(4));
        }

        [Test]
        public void GetSupportedNetworks_ReturnsNonEmptyList()
        {
            // Arrange
            _serviceMock.Setup(s => s.GetSupportedNetworks())
                .Returns(new List<string> { "algorand-mainnet" }.AsReadOnly());

            // Act
            var result = _controller.GetSupportedNetworks() as OkObjectResult;
            var response = result!.Value as SupportedNetworksResponse;

            // Assert
            Assert.That(response!.Networks, Is.Not.Empty);
        }

        [Test]
        public void GetSupportedNetworks_ServiceThrows_Returns500()
        {
            // Arrange
            _serviceMock.Setup(s => s.GetSupportedNetworks())
                .Throws(new Exception("Service error"));

            // Act
            var result = _controller.GetSupportedNetworks() as ObjectResult;

            // Assert
            Assert.That(result!.StatusCode, Is.EqualTo(500));
        }

        // ── ValidateAddress ───────────────────────────────────────────────────

        [Test]
        public void ValidateAddress_ValidAlgorandAddress_ReturnsOkWithIsValidTrue()
        {
            // Arrange
            _serviceMock.Setup(s => s.ValidateWalletAddress(AlgorandAddress, MainNet)).Returns(true);
            var request = new WalletAddressValidationRequest { Address = AlgorandAddress, Network = MainNet };

            // Act
            var result = _controller.ValidateAddress(request) as OkObjectResult;

            // Assert
            Assert.That(result, Is.Not.Null);
            var response = result!.Value as WalletAddressValidationResponse;
            Assert.That(response!.IsValid, Is.True);
            Assert.That(response.Address, Is.EqualTo(AlgorandAddress));
            Assert.That(response.Network, Is.EqualTo(MainNet));
        }

        [Test]
        public void ValidateAddress_InvalidAddress_ReturnsOkWithIsValidFalse()
        {
            // Arrange
            const string badAddress = "not-a-valid-address";
            _serviceMock.Setup(s => s.ValidateWalletAddress(badAddress, MainNet)).Returns(false);
            var request = new WalletAddressValidationRequest { Address = badAddress, Network = MainNet };

            // Act
            var result = _controller.ValidateAddress(request) as OkObjectResult;

            // Assert
            Assert.That(result, Is.Not.Null);
            var response = result!.Value as WalletAddressValidationResponse;
            Assert.That(response!.IsValid, Is.False);
        }

        [Test]
        public void ValidateAddress_EmptyAddress_ReturnsBadRequest()
        {
            // Arrange
            var request = new WalletAddressValidationRequest { Address = "", Network = MainNet };

            // Act
            var result = _controller.ValidateAddress(request) as BadRequestObjectResult;

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.StatusCode, Is.EqualTo(400));
            _serviceMock.Verify(s => s.ValidateWalletAddress(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Test]
        public void ValidateAddress_EmptyNetwork_ReturnsBadRequest()
        {
            // Arrange
            var request = new WalletAddressValidationRequest { Address = AlgorandAddress, Network = "" };

            // Act
            var result = _controller.ValidateAddress(request) as BadRequestObjectResult;

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.StatusCode, Is.EqualTo(400));
        }

        [Test]
        public void ValidateAddress_ValidEvmAddress_ReturnsOkWithIsValidTrue()
        {
            // Arrange
            _serviceMock.Setup(s => s.ValidateWalletAddress(EvmAddress, "base-mainnet")).Returns(true);
            var request = new WalletAddressValidationRequest { Address = EvmAddress, Network = "base-mainnet" };

            // Act
            var result = _controller.ValidateAddress(request) as OkObjectResult;

            // Assert
            Assert.That(result, Is.Not.Null);
            var response = result!.Value as WalletAddressValidationResponse;
            Assert.That(response!.IsValid, Is.True);
        }

        [Test]
        public void ValidateAddress_ServiceThrows_Returns500()
        {
            // Arrange
            _serviceMock.Setup(s => s.ValidateWalletAddress(It.IsAny<string>(), It.IsAny<string>()))
                .Throws(new Exception("Service error"));
            var request = new WalletAddressValidationRequest { Address = AlgorandAddress, Network = MainNet };

            // Act
            var result = _controller.ValidateAddress(request) as ObjectResult;

            // Assert
            Assert.That(result!.StatusCode, Is.EqualTo(500));
        }

        // ── Request model validation ──────────────────────────────────────────

        [Test]
        public void WalletConnectRequest_DefaultValues_AreCorrect()
        {
            var request = new WalletConnectRequest();
            Assert.That(request.Address, Is.EqualTo(string.Empty));
            Assert.That(request.ActualNetwork, Is.EqualTo(string.Empty));
            Assert.That(request.ExpectedNetwork, Is.Null);
        }

        [Test]
        public void WalletDisconnectRequest_DefaultValues_AreCorrect()
        {
            var request = new WalletDisconnectRequest();
            Assert.That(request.Address, Is.EqualTo(string.Empty));
        }

        [Test]
        public void WalletReconnectRequest_DefaultValues_AreCorrect()
        {
            var request = new WalletReconnectRequest();
            Assert.That(request.Address, Is.EqualTo(string.Empty));
            Assert.That(request.ActualNetwork, Is.EqualTo(string.Empty));
            Assert.That(request.ExpectedNetwork, Is.Null);
        }

        [Test]
        public void SupportedNetworksResponse_TotalCount_MatchesNetworksCount()
        {
            var response = new SupportedNetworksResponse
            {
                Networks = new List<string> { "net1", "net2", "net3" }
            };
            Assert.That(response.TotalCount, Is.EqualTo(3));
        }

        [Test]
        public void WalletAddressValidationResponse_DefaultValues_AreCorrect()
        {
            var response = new WalletAddressValidationResponse();
            Assert.That(response.IsValid, Is.False);
            Assert.That(response.Address, Is.EqualTo(string.Empty));
            Assert.That(response.Network, Is.EqualTo(string.Empty));
            Assert.That(response.Message, Is.EqualTo(string.Empty));
        }

        // ── Determinism: same input always produces same output ───────────────

        [Test]
        public void ValidateAddress_Determinism_SameInputProducesSameResult()
        {
            // Arrange
            _serviceMock.Setup(s => s.ValidateWalletAddress(AlgorandAddress, MainNet)).Returns(true);
            var request = new WalletAddressValidationRequest { Address = AlgorandAddress, Network = MainNet };

            // Act – three consecutive calls
            var r1 = (_controller.ValidateAddress(request) as OkObjectResult)!.Value as WalletAddressValidationResponse;
            var r2 = (_controller.ValidateAddress(request) as OkObjectResult)!.Value as WalletAddressValidationResponse;
            var r3 = (_controller.ValidateAddress(request) as OkObjectResult)!.Value as WalletAddressValidationResponse;

            // Assert
            Assert.That(r1!.IsValid, Is.EqualTo(r2!.IsValid));
            Assert.That(r2.IsValid, Is.EqualTo(r3!.IsValid));
        }

        [Test]
        public void Connect_Determinism_SameInputProducesSameResult()
        {
            // Arrange
            var state = new WalletConnectionState { WalletAddress = AlgorandAddress, Status = WalletConnectionStatus.Connected };
            _serviceMock.Setup(s => s.Connect(AlgorandAddress, MainNet, MainNet)).Returns(state);
            var request = new WalletConnectRequest { Address = AlgorandAddress, ActualNetwork = MainNet, ExpectedNetwork = MainNet };

            // Act – three consecutive calls
            var r1 = (_controller.Connect(request) as OkObjectResult)!.Value as WalletConnectionState;
            var r2 = (_controller.Connect(request) as OkObjectResult)!.Value as WalletConnectionState;
            var r3 = (_controller.Connect(request) as OkObjectResult)!.Value as WalletConnectionState;

            // Assert
            Assert.That(r1!.Status, Is.EqualTo(r2!.Status));
            Assert.That(r2.Status, Is.EqualTo(r3!.Status));
        }

        // ── Security: no sensitive data leaked on error ───────────────────────

        [Test]
        public void GetConnectionState_OnError_DoesNotLeakInternalMessage()
        {
            // Arrange
            _serviceMock.Setup(s => s.GetConnectionState(It.IsAny<string>(), It.IsAny<string>()))
                .Throws(new InvalidOperationException("Internal database connection string: Server=prod-db;Password=secret123"));

            // Act
            var result = _controller.GetConnectionState(AlgorandAddress, MainNet) as ObjectResult;

            // Assert
            Assert.That(result!.StatusCode, Is.EqualTo(500));
            // The controller must NOT expose internal exception messages in the response
            var body = result.Value?.ToString() ?? string.Empty;
            Assert.That(body, Does.Not.Contain("secret123"), "Internal secrets must not be leaked in error response");
        }

        [Test]
        public void ValidateAddress_MessageIsUserFriendly_ForValidAddress()
        {
            // Arrange
            _serviceMock.Setup(s => s.ValidateWalletAddress(AlgorandAddress, MainNet)).Returns(true);
            var request = new WalletAddressValidationRequest { Address = AlgorandAddress, Network = MainNet };

            // Act
            var result = (_controller.ValidateAddress(request) as OkObjectResult)!.Value as WalletAddressValidationResponse;

            // Assert
            Assert.That(result!.Message, Is.Not.Empty);
            Assert.That(result.Message, Does.Contain("valid"));
        }

        [Test]
        public void ValidateAddress_MessageIsUserFriendly_ForInvalidAddress()
        {
            // Arrange
            _serviceMock.Setup(s => s.ValidateWalletAddress("bad", MainNet)).Returns(false);
            var request = new WalletAddressValidationRequest { Address = "bad", Network = MainNet };

            // Act
            var result = (_controller.ValidateAddress(request) as OkObjectResult)!.Value as WalletAddressValidationResponse;

            // Assert
            Assert.That(result!.Message, Is.Not.Empty);
            Assert.That(result.Message, Does.Contain("invalid"));
        }
    }
}

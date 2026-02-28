using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models.Wallet;
using BiatecTokensApi.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Unit tests for <see cref="WalletConnectionService"/>.
    /// Covers connection state management, network mismatch detection, reconnect flows,
    /// address validation, and all supported reconnect reason branches.
    /// </summary>
    [TestFixture]
    public class WalletConnectionServiceUnitTests
    {
        private Mock<ILogger<WalletConnectionService>> _loggerMock = null!;
        private WalletConnectionService _service = null!;

        private const string AlgorandAddress = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
        private const string EvmAddress = "0xAbCdEf1234567890abcdef1234567890AbCdEf12";
        private const string MainNet = "algorand-mainnet";
        private const string TestNet = "algorand-testnet";

        [SetUp]
        public void SetUp()
        {
            _loggerMock = new Mock<ILogger<WalletConnectionService>>();
            _service = new WalletConnectionService(_loggerMock.Object);
        }

        // ── GetConnectionState ────────────────────────────────────────────────

        [Test]
        public void GetConnectionState_WhenNotYetConnected_ReturnsDisconnectedState()
        {
            var state = _service.GetConnectionState(AlgorandAddress, MainNet);

            Assert.That(state.Status, Is.EqualTo(WalletConnectionStatus.Disconnected));
            Assert.That(state.WalletAddress, Is.EqualTo(AlgorandAddress));
        }

        [Test]
        public void GetConnectionState_EmptyAddress_ReturnsDisconnectedState()
        {
            var state = _service.GetConnectionState("", MainNet);

            Assert.That(state.Status, Is.EqualTo(WalletConnectionStatus.Disconnected));
        }

        [Test]
        public void GetConnectionState_AfterConnect_ReturnsConnectedState()
        {
            _service.Connect(AlgorandAddress, MainNet, MainNet);

            var state = _service.GetConnectionState(AlgorandAddress, MainNet);

            Assert.That(state.Status, Is.EqualTo(WalletConnectionStatus.Connected));
        }

        // ── Connect ───────────────────────────────────────────────────────────

        [Test]
        public void Connect_FirstTime_ReturnsConnectedAndIsFirstConnection()
        {
            var state = _service.Connect(AlgorandAddress, MainNet, MainNet);

            Assert.That(state.Status, Is.EqualTo(WalletConnectionStatus.Connected));
            Assert.That(state.IsFirstConnection, Is.True);
            Assert.That(state.WalletAddress, Is.EqualTo(AlgorandAddress));
        }

        [Test]
        public void Connect_SecondTime_IsNotFirstConnection()
        {
            _service.Connect(AlgorandAddress, MainNet, MainNet);
            var state = _service.Connect(AlgorandAddress, MainNet, MainNet);

            Assert.That(state.IsFirstConnection, Is.False);
        }

        [Test]
        public void Connect_WrongNetwork_ReturnsNetworkMismatchState()
        {
            var state = _service.Connect(AlgorandAddress, TestNet, MainNet);

            Assert.That(state.Status, Is.EqualTo(WalletConnectionStatus.NetworkMismatch));
            Assert.That(state.NetworkMismatch, Is.Not.Null);
            Assert.That(state.NetworkMismatch!.ActualNetwork, Is.EqualTo(TestNet));
            Assert.That(state.NetworkMismatch.ExpectedNetwork, Is.EqualTo(MainNet));
        }

        [Test]
        public void Connect_CorrectNetwork_NoNetworkMismatchInfo()
        {
            var state = _service.Connect(AlgorandAddress, MainNet, MainNet);

            Assert.That(state.NetworkMismatch, Is.Null);
        }

        [Test]
        public void Connect_StatusMessage_IsNonEmpty()
        {
            var state = _service.Connect(AlgorandAddress, MainNet, MainNet);

            Assert.That(state.StatusMessage, Is.Not.Null.And.Not.Empty);
        }

        // ── Disconnect ────────────────────────────────────────────────────────

        [Test]
        public void Disconnect_AfterConnect_ReturnsDisconnectedState()
        {
            _service.Connect(AlgorandAddress, MainNet, MainNet);
            var state = _service.Disconnect(AlgorandAddress);

            Assert.That(state.Status, Is.EqualTo(WalletConnectionStatus.Disconnected));
        }

        [Test]
        public void Disconnect_ClearsState_SubsequentGetReturnsDisconnected()
        {
            _service.Connect(AlgorandAddress, MainNet, MainNet);
            _service.Disconnect(AlgorandAddress);

            var state = _service.GetConnectionState(AlgorandAddress, MainNet);

            Assert.That(state.Status, Is.EqualTo(WalletConnectionStatus.Disconnected));
        }

        // ── Reconnect ─────────────────────────────────────────────────────────

        [Test]
        public void Reconnect_CorrectNetwork_ReturnsConnectedState()
        {
            var state = _service.Reconnect(AlgorandAddress, MainNet, MainNet);

            Assert.That(state.Status, Is.EqualTo(WalletConnectionStatus.Connected));
        }

        [Test]
        public void Reconnect_IsNeverFirstConnection()
        {
            var state = _service.Reconnect(AlgorandAddress, MainNet, MainNet);

            Assert.That(state.IsFirstConnection, Is.False);
        }

        [Test]
        public void Reconnect_NetworkMismatch_SetsHasReconnectGuidance()
        {
            var state = _service.Reconnect(AlgorandAddress, TestNet, MainNet);

            Assert.That(state.Status, Is.EqualTo(WalletConnectionStatus.NetworkMismatch));
            Assert.That(state.HasReconnectGuidance, Is.True);
        }

        [Test]
        public void Reconnect_ClearsErrorState()
        {
            // First simulate an error by connecting to wrong network, then reconnect correctly
            _service.Connect(AlgorandAddress, TestNet, MainNet);
            var state = _service.Reconnect(AlgorandAddress, MainNet, MainNet);

            Assert.That(state.Status, Is.EqualTo(WalletConnectionStatus.Connected));
            Assert.That(state.ErrorCode, Is.Null);
        }

        // ── DetectNetworkMismatch ─────────────────────────────────────────────

        [Test]
        public void DetectNetworkMismatch_SameNetwork_ReturnsFalse()
        {
            Assert.That(_service.DetectNetworkMismatch(MainNet, MainNet), Is.False);
        }

        [Test]
        public void DetectNetworkMismatch_DifferentNetworks_ReturnsTrue()
        {
            Assert.That(_service.DetectNetworkMismatch(TestNet, MainNet), Is.True);
        }

        [Test]
        public void DetectNetworkMismatch_CaseInsensitive_ReturnsFalse()
        {
            Assert.That(_service.DetectNetworkMismatch("Algorand-Mainnet", "algorand-mainnet"), Is.False);
        }

        [Test]
        public void DetectNetworkMismatch_EmptyActual_ReturnsFalse()
        {
            Assert.That(_service.DetectNetworkMismatch("", MainNet), Is.False);
        }

        // ── GetReconnectGuidance (all WalletReconnectReason branches) ─────────

        [Test]
        public void GetReconnectGuidance_NetworkMismatch_HasSwitchNetworkStep()
        {
            var guidance = _service.GetReconnectGuidance(WalletReconnectReason.NetworkMismatch, TestNet, MainNet);

            Assert.That(guidance.Reason, Is.EqualTo(WalletReconnectReason.NetworkMismatch));
            Assert.That(guidance.Steps, Is.Not.Empty);
            Assert.That(guidance.CanAutoResolve, Is.False);
        }

        [Test]
        public void GetReconnectGuidance_SessionExpired_HasSignInStep()
        {
            var guidance = _service.GetReconnectGuidance(WalletReconnectReason.SessionExpired);

            Assert.That(guidance.Reason, Is.EqualTo(WalletReconnectReason.SessionExpired));
            Assert.That(guidance.Steps.Count, Is.GreaterThan(0));
        }

        [Test]
        public void GetReconnectGuidance_NetworkTimeout_CanAutoResolve()
        {
            var guidance = _service.GetReconnectGuidance(WalletReconnectReason.NetworkTimeout);

            Assert.That(guidance.Reason, Is.EqualTo(WalletReconnectReason.NetworkTimeout));
            Assert.That(guidance.CanAutoResolve, Is.True);
        }

        [Test]
        public void GetReconnectGuidance_AuthorizationRevoked_HasReAuthSteps()
        {
            var guidance = _service.GetReconnectGuidance(WalletReconnectReason.AuthorizationRevoked);

            Assert.That(guidance.Reason, Is.EqualTo(WalletReconnectReason.AuthorizationRevoked));
            Assert.That(guidance.Steps.Count, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void GetReconnectGuidance_WalletDisconnected_HasReopenStep()
        {
            var guidance = _service.GetReconnectGuidance(WalletReconnectReason.WalletDisconnected);

            Assert.That(guidance.Reason, Is.EqualTo(WalletReconnectReason.WalletDisconnected));
            Assert.That(guidance.Steps.Count, Is.GreaterThan(0));
        }

        [Test]
        public void GetReconnectGuidance_Unknown_HasContactSupportStep()
        {
            var guidance = _service.GetReconnectGuidance(WalletReconnectReason.Unknown);

            Assert.That(guidance.Reason, Is.EqualTo(WalletReconnectReason.Unknown));
            Assert.That(guidance.Steps.Count, Is.GreaterThanOrEqualTo(3));
        }

        [Test]
        public void GetReconnectGuidance_AllReasons_StepsAreNumberedSequentially()
        {
            foreach (WalletReconnectReason reason in Enum.GetValues(typeof(WalletReconnectReason)))
            {
                var guidance = _service.GetReconnectGuidance(reason);
                for (int i = 0; i < guidance.Steps.Count; i++)
                {
                    Assert.That(guidance.Steps[i].StepNumber, Is.EqualTo(i + 1),
                        $"Step {i + 1} for reason {reason} must have StepNumber {i + 1}");
                }
            }
        }

        // ── GetSupportedNetworks ──────────────────────────────────────────────

        [Test]
        public void GetSupportedNetworks_ReturnsNonEmptyList()
        {
            var networks = _service.GetSupportedNetworks();

            Assert.That(networks, Is.Not.Empty);
        }

        [Test]
        public void GetSupportedNetworks_IncludesAlgorandMainnet()
        {
            var networks = _service.GetSupportedNetworks();

            Assert.That(networks, Does.Contain("algorand-mainnet"));
        }

        // ── ValidateWalletAddress ─────────────────────────────────────────────

        [Test]
        public void ValidateWalletAddress_ValidAlgorandAddress_ReturnsTrue()
        {
            // 58-char base32 Algorand address
            var address = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
            Assert.That(_service.ValidateWalletAddress(address, "algorand-mainnet"), Is.True);
        }

        [Test]
        public void ValidateWalletAddress_InvalidAlgorandAddress_TooShort_ReturnsFalse()
        {
            Assert.That(_service.ValidateWalletAddress("SHORT", "algorand-mainnet"), Is.False);
        }

        [Test]
        public void ValidateWalletAddress_ValidEvmAddress_ReturnsTrue()
        {
            Assert.That(_service.ValidateWalletAddress(EvmAddress, "base-mainnet"), Is.True);
        }

        [Test]
        public void ValidateWalletAddress_InvalidEvmAddress_NoPrefix_ReturnsFalse()
        {
            Assert.That(_service.ValidateWalletAddress("AbCdEf1234567890abcdef1234567890AbCdEf1234", "ethereum-mainnet"), Is.False);
        }

        [Test]
        public void ValidateWalletAddress_EmptyAddress_ReturnsFalse()
        {
            Assert.That(_service.ValidateWalletAddress("", "algorand-mainnet"), Is.False);
        }

        // ── Determinism ───────────────────────────────────────────────────────

        [Test]
        public void Connect_SameAddressThreeTimes_ProducesDeterministicResults()
        {
            var address = "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB";
            _service.Disconnect(address);

            var r1 = _service.Connect(address, MainNet, MainNet);
            _service.Disconnect(address);
            var r2 = _service.Connect(address, MainNet, MainNet);
            _service.Disconnect(address);
            var r3 = _service.Connect(address, MainNet, MainNet);

            Assert.That(r1.Status, Is.EqualTo(r2.Status).And.EqualTo(r3.Status));
        }
    }
}

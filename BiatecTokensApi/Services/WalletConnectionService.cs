using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models.Wallet;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Service for managing wallet connection state, reconnection flows, and network mismatch detection
    /// </summary>
    public class WalletConnectionService : IWalletConnectionService
    {
        private readonly ILogger<WalletConnectionService> _logger;

        // In-memory state store keyed by wallet address; sufficient for stateless backend use
        private readonly ConcurrentDictionary<string, WalletConnectionState> _states = new();

        // Wallets seen for the first time (for onboarding guidance)
        private readonly ConcurrentDictionary<string, bool> _knownWallets = new();

        private static readonly IReadOnlyList<string> _supportedNetworks = new List<string>
        {
            "algorand-mainnet",
            "algorand-testnet",
            "algorand-betanet",
            "voi-mainnet",
            "aramid-mainnet",
            "base-mainnet",
            "ethereum-mainnet"
        }.AsReadOnly();

        /// <summary>
        /// Initializes a new instance of the <see cref="WalletConnectionService"/> class.
        /// </summary>
        public WalletConnectionService(ILogger<WalletConnectionService> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc/>
        public WalletConnectionState GetConnectionState(string walletAddress, string expectedNetwork)
        {
            if (string.IsNullOrWhiteSpace(walletAddress))
            {
                return new WalletConnectionState
                {
                    Status = WalletConnectionStatus.Disconnected,
                    StatusMessage = "No wallet address provided"
                };
            }

            if (_states.TryGetValue(walletAddress, out var existing))
            {
                existing.ExpectedNetwork = expectedNetwork;
                return existing;
            }

            return new WalletConnectionState
            {
                WalletAddress = walletAddress,
                Status = WalletConnectionStatus.Disconnected,
                ExpectedNetwork = expectedNetwork,
                StatusMessage = "Wallet has not connected in this session"
            };
        }

        /// <inheritdoc/>
        public WalletConnectionState Connect(string walletAddress, string actualNetwork, string expectedNetwork)
        {
            var isFirst = _knownWallets.TryAdd(walletAddress, true);
            var hasMismatch = DetectNetworkMismatch(actualNetwork, expectedNetwork);

            var state = new WalletConnectionState
            {
                WalletAddress = walletAddress,
                ActualNetwork = actualNetwork,
                ExpectedNetwork = expectedNetwork,
                ConnectedAt = DateTime.UtcNow,
                LastAttemptAt = DateTime.UtcNow,
                IsFirstConnection = isFirst,
                HasReconnectGuidance = false
            };

            if (hasMismatch)
            {
                state.Status = WalletConnectionStatus.NetworkMismatch;
                state.StatusMessage = $"Connected to {actualNetwork} but {expectedNetwork} is required";
                state.NetworkMismatch = BuildNetworkMismatchInfo(actualNetwork, expectedNetwork);
                _logger.LogWarning("Wallet {Address} connected on {Actual} but {Expected} is required",
                    LoggingHelper.SanitizeLogInput(walletAddress),
                    LoggingHelper.SanitizeLogInput(actualNetwork),
                    LoggingHelper.SanitizeLogInput(expectedNetwork));
            }
            else
            {
                state.Status = WalletConnectionStatus.Connected;
                state.StatusMessage = isFirst
                    ? "Wallet connected for the first time — welcome!"
                    : "Wallet connected successfully";
                _logger.LogInformation("Wallet {Address} connected on {Network}",
                    LoggingHelper.SanitizeLogInput(walletAddress),
                    LoggingHelper.SanitizeLogInput(actualNetwork));
            }

            _states[walletAddress] = state;
            return state;
        }

        /// <inheritdoc/>
        public WalletConnectionState Disconnect(string walletAddress)
        {
            _states.TryRemove(walletAddress, out _);

            var state = new WalletConnectionState
            {
                WalletAddress = walletAddress,
                Status = WalletConnectionStatus.Disconnected,
                StatusMessage = "Wallet disconnected",
                LastAttemptAt = DateTime.UtcNow
            };

            _logger.LogInformation("Wallet {Address} disconnected",
                LoggingHelper.SanitizeLogInput(walletAddress));
            return state;
        }

        /// <inheritdoc/>
        public WalletConnectionState Reconnect(string walletAddress, string actualNetwork, string expectedNetwork)
        {
            // Mark as reconnecting first
            var reconnecting = new WalletConnectionState
            {
                WalletAddress = walletAddress,
                Status = WalletConnectionStatus.Reconnecting,
                ActualNetwork = actualNetwork,
                ExpectedNetwork = expectedNetwork,
                StatusMessage = "Reconnecting wallet…",
                LastAttemptAt = DateTime.UtcNow
            };
            _states[walletAddress] = reconnecting;

            // Re-use Connect logic (clears error state, detects mismatch)
            var result = Connect(walletAddress, actualNetwork, expectedNetwork);

            // Reconnection is never a "first" connection
            result.IsFirstConnection = false;
            result.HasReconnectGuidance = result.Status != WalletConnectionStatus.Connected;

            if (result.Status == WalletConnectionStatus.Connected)
            {
                result.StatusMessage = "Wallet reconnected successfully";
            }

            _states[walletAddress] = result;
            return result;
        }

        /// <inheritdoc/>
        public bool DetectNetworkMismatch(string actualNetwork, string expectedNetwork)
        {
            if (string.IsNullOrWhiteSpace(actualNetwork) || string.IsNullOrWhiteSpace(expectedNetwork))
                return false;

            return !string.Equals(actualNetwork.Trim(), expectedNetwork.Trim(),
                StringComparison.OrdinalIgnoreCase);
        }

        /// <inheritdoc/>
        public WalletReconnectGuidance GetReconnectGuidance(
            WalletReconnectReason reason,
            string? actualNetwork = null,
            string? expectedNetwork = null)
        {
            return reason switch
            {
                WalletReconnectReason.NetworkMismatch => BuildNetworkMismatchGuidance(actualNetwork, expectedNetwork),
                WalletReconnectReason.SessionExpired => BuildSessionExpiredGuidance(),
                WalletReconnectReason.NetworkTimeout => BuildNetworkTimeoutGuidance(),
                WalletReconnectReason.AuthorizationRevoked => BuildAuthorizationRevokedGuidance(),
                WalletReconnectReason.WalletDisconnected => BuildWalletDisconnectedGuidance(),
                _ => BuildUnknownReasonGuidance()
            };
        }

        /// <inheritdoc/>
        public IReadOnlyList<string> GetSupportedNetworks() => _supportedNetworks;

        /// <inheritdoc/>
        /// <remarks>
        /// This performs structural validation only (length and character set for Algorand;
        /// prefix and hex characters for EVM). It does not verify the Algorand 4-byte
        /// checksum or the EVM EIP-55 mixed-case checksum. Use this for fast client-side
        /// format checking; rely on on-chain confirmation for authoritative validity.
        /// </remarks>
        public bool ValidateWalletAddress(string walletAddress, string network)
        {
            if (string.IsNullOrWhiteSpace(walletAddress))
                return false;

            // Algorand: 58-character base32 address
            if (network.StartsWith("algorand", StringComparison.OrdinalIgnoreCase) ||
                network.StartsWith("voi", StringComparison.OrdinalIgnoreCase) ||
                network.StartsWith("aramid", StringComparison.OrdinalIgnoreCase))
            {
                return walletAddress.Length == 58 &&
                       walletAddress.All(c => "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567".Contains(c));
            }

            // EVM: 0x + 40 hex chars
            if (network.StartsWith("base", StringComparison.OrdinalIgnoreCase) ||
                network.StartsWith("ethereum", StringComparison.OrdinalIgnoreCase))
            {
                return walletAddress.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
                       walletAddress.Length == 42 &&
                       walletAddress[2..].All(c => Uri.IsHexDigit(c));
            }

            // Unknown network: accept non-empty address
            return !string.IsNullOrWhiteSpace(walletAddress);
        }

        // ── Private helpers ──────────────────────────────────────────────────────

        private static NetworkMismatchInfo BuildNetworkMismatchInfo(string actualNetwork, string expectedNetwork)
        {
            return new NetworkMismatchInfo
            {
                ActualNetwork = actualNetwork,
                ExpectedNetwork = expectedNetwork,
                Description = $"Your wallet is on '{actualNetwork}' but this application requires '{expectedNetwork}'.",
                ResolutionGuidance = $"Switch your wallet to '{expectedNetwork}' and reconnect."
            };
        }

        private static WalletReconnectGuidance BuildNetworkMismatchGuidance(
            string? actualNetwork, string? expectedNetwork)
        {
            return new WalletReconnectGuidance
            {
                Reason = WalletReconnectReason.NetworkMismatch,
                Explanation = $"Your wallet is connected to '{actualNetwork ?? "unknown"}' " +
                              $"but this application requires '{expectedNetwork ?? "unknown"}'.",
                CanAutoResolve = false,
                EstimatedResolutionSeconds = 30,
                Steps = new List<WalletReconnectStep>
                {
                    new() { StepNumber = 1, Title = "Open your wallet", Instruction = "Open the wallet application or browser extension.", IsAutomated = false },
                    new() { StepNumber = 2, Title = "Switch network", Instruction = $"Change the active network to '{expectedNetwork ?? "the required network"}'.", IsAutomated = false },
                    new() { StepNumber = 3, Title = "Reconnect", Instruction = "Return to this application and reconnect your wallet.", IsAutomated = true }
                }
            };
        }

        private static WalletReconnectGuidance BuildSessionExpiredGuidance()
        {
            return new WalletReconnectGuidance
            {
                Reason = WalletReconnectReason.SessionExpired,
                Explanation = "Your session has expired. Please sign in again to continue.",
                CanAutoResolve = false,
                EstimatedResolutionSeconds = 60,
                Steps = new List<WalletReconnectStep>
                {
                    new() { StepNumber = 1, Title = "Reconnect wallet", Instruction = "Click 'Connect Wallet' to start a new session.", IsAutomated = false },
                    new() { StepNumber = 2, Title = "Approve sign-in", Instruction = "Approve the authentication request in your wallet.", IsAutomated = false }
                }
            };
        }

        private static WalletReconnectGuidance BuildNetworkTimeoutGuidance()
        {
            return new WalletReconnectGuidance
            {
                Reason = WalletReconnectReason.NetworkTimeout,
                Explanation = "A temporary network issue interrupted the connection. Retrying is safe.",
                CanAutoResolve = true,
                EstimatedResolutionSeconds = 10,
                Steps = new List<WalletReconnectStep>
                {
                    new() { StepNumber = 1, Title = "Wait briefly", Instruction = "Wait a few seconds for the network to stabilise.", IsAutomated = false },
                    new() { StepNumber = 2, Title = "Retry connection", Instruction = "Click 'Reconnect' to try again.", IsAutomated = true }
                }
            };
        }

        private static WalletReconnectGuidance BuildAuthorizationRevokedGuidance()
        {
            return new WalletReconnectGuidance
            {
                Reason = WalletReconnectReason.AuthorizationRevoked,
                Explanation = "Wallet authorization was revoked. Grant access again to continue.",
                CanAutoResolve = false,
                EstimatedResolutionSeconds = 60,
                Steps = new List<WalletReconnectStep>
                {
                    new() { StepNumber = 1, Title = "Open wallet settings", Instruction = "Open the connected apps or permissions section of your wallet.", IsAutomated = false },
                    new() { StepNumber = 2, Title = "Re-authorize", Instruction = "Grant this application permission to connect.", IsAutomated = false },
                    new() { StepNumber = 3, Title = "Reconnect", Instruction = "Return here and reconnect.", IsAutomated = true }
                }
            };
        }

        private static WalletReconnectGuidance BuildWalletDisconnectedGuidance()
        {
            return new WalletReconnectGuidance
            {
                Reason = WalletReconnectReason.WalletDisconnected,
                Explanation = "Your wallet was closed or disconnected. Reconnect to resume.",
                CanAutoResolve = false,
                EstimatedResolutionSeconds = 30,
                Steps = new List<WalletReconnectStep>
                {
                    new() { StepNumber = 1, Title = "Open wallet", Instruction = "Reopen your wallet application.", IsAutomated = false },
                    new() { StepNumber = 2, Title = "Connect wallet", Instruction = "Click 'Connect Wallet' on this page.", IsAutomated = false }
                }
            };
        }

        private static WalletReconnectGuidance BuildUnknownReasonGuidance()
        {
            return new WalletReconnectGuidance
            {
                Reason = WalletReconnectReason.Unknown,
                Explanation = "An unexpected issue interrupted the wallet connection.",
                CanAutoResolve = false,
                EstimatedResolutionSeconds = 60,
                Steps = new List<WalletReconnectStep>
                {
                    new() { StepNumber = 1, Title = "Refresh page", Instruction = "Refresh this page and try again.", IsAutomated = false },
                    new() { StepNumber = 2, Title = "Connect wallet", Instruction = "Click 'Connect Wallet' to start fresh.", IsAutomated = false },
                    new() { StepNumber = 3, Title = "Contact support", Instruction = "If the issue persists, contact support with your correlation ID.", IsAutomated = false }
                }
            };
        }
    }
}

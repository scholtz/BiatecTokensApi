using BiatecTokensApi.Models.Wallet;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service for managing wallet connection state, reconnection flows, and network mismatch detection
    /// </summary>
    public interface IWalletConnectionService
    {
        /// <summary>
        /// Gets the current connection state for a wallet address
        /// </summary>
        /// <param name="walletAddress">Wallet address to query</param>
        /// <param name="expectedNetwork">Network the application requires</param>
        /// <returns>Current wallet connection state</returns>
        WalletConnectionState GetConnectionState(string walletAddress, string expectedNetwork);

        /// <summary>
        /// Records a successful wallet connection and returns the updated state
        /// </summary>
        /// <param name="walletAddress">Connected wallet address</param>
        /// <param name="actualNetwork">Network the wallet is connected to</param>
        /// <param name="expectedNetwork">Network the application requires</param>
        /// <returns>Connection state after connecting (may be Connected or NetworkMismatch)</returns>
        WalletConnectionState Connect(string walletAddress, string actualNetwork, string expectedNetwork);

        /// <summary>
        /// Records a disconnect event and returns the disconnected state
        /// </summary>
        /// <param name="walletAddress">Wallet address to disconnect</param>
        /// <returns>Disconnected connection state</returns>
        WalletConnectionState Disconnect(string walletAddress);

        /// <summary>
        /// Attempts to reconnect a previously connected wallet
        /// </summary>
        /// <param name="walletAddress">Wallet address to reconnect</param>
        /// <param name="actualNetwork">Network the wallet is currently on</param>
        /// <param name="expectedNetwork">Network the application requires</param>
        /// <returns>Connection state after reconnection attempt</returns>
        WalletConnectionState Reconnect(string walletAddress, string actualNetwork, string expectedNetwork);

        /// <summary>
        /// Detects whether the wallet is on the wrong network
        /// </summary>
        /// <param name="actualNetwork">Network the wallet is currently on</param>
        /// <param name="expectedNetwork">Network the application requires</param>
        /// <returns>True if a network mismatch is detected</returns>
        bool DetectNetworkMismatch(string actualNetwork, string expectedNetwork);

        /// <summary>
        /// Returns step-by-step reconnect guidance for a given failure reason
        /// </summary>
        /// <param name="reason">Why reconnection is needed</param>
        /// <param name="actualNetwork">Network the wallet is currently on (optional)</param>
        /// <param name="expectedNetwork">Network the application requires (optional)</param>
        /// <returns>Ordered reconnect guidance with actionable steps</returns>
        WalletReconnectGuidance GetReconnectGuidance(
            WalletReconnectReason reason,
            string? actualNetwork = null,
            string? expectedNetwork = null);

        /// <summary>
        /// Returns the list of supported blockchain networks
        /// </summary>
        /// <returns>List of supported network identifiers</returns>
        IReadOnlyList<string> GetSupportedNetworks();

        /// <summary>
        /// Validates that a wallet address has the correct format for the given network
        /// </summary>
        /// <param name="walletAddress">Wallet address to validate</param>
        /// <param name="network">Network to validate against</param>
        /// <returns>True if the address is valid for the network</returns>
        bool ValidateWalletAddress(string walletAddress, string network);
    }
}

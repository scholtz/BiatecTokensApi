using System.Security.Cryptography;
using System.Text;
using BiatecTokensApi.Models.TokenDeploymentLifecycle;
using BiatecTokensApi.Services.Interface;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Returns deterministic hash-derived blockchain evidence suitable for contract-shape
    /// validation, development, and test environments.
    ///
    /// All fields are computed via SHA-256 from the deployment ID to ensure idempotency:
    /// the same deployment ID always produces the same evidence values.
    ///
    /// <see cref="BlockchainDeploymentEvidence.IsSimulated"/> is always <c>true</c>.
    /// Sign-off tooling MUST check this flag before treating the evidence as production-valid.
    /// </summary>
    public sealed class SimulatedDeploymentEvidenceProvider : IDeploymentEvidenceProvider
    {
        /// <inheritdoc/>
        public bool IsSimulated => true;

        /// <inheritdoc/>
        public Task<BlockchainDeploymentEvidence?> ObtainEvidenceAsync(
            string deploymentId,
            string tokenStandard,
            string network,
            CancellationToken cancellationToken = default)
        {
            var txId = DeriveTransactionId(deploymentId, tokenStandard);
            var assetId = DeriveAssetId(deploymentId);
            var confirmedRound = DeriveConfirmedRound(deploymentId);

            var evidence = new BlockchainDeploymentEvidence
            {
                TransactionId  = txId,
                AssetId        = assetId,
                ConfirmedRound = confirmedRound,
                IsSimulated    = true,
                EvidenceSource = "simulation",
                ObtainedAt     = DateTimeOffset.UtcNow,
            };

            return Task.FromResult<BlockchainDeploymentEvidence?>(evidence);
        }

        private static string DeriveTransactionId(string deploymentId, string standard)
        {
            var input = $"txid:{deploymentId}:{standard}";
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes)[..32].ToLowerInvariant();
        }

        private static ulong DeriveAssetId(string deploymentId)
        {
            var input = $"assetid:{deploymentId}";
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return BitConverter.ToUInt64(bytes, 0) % 1_000_000_000UL + 1_000_000UL;
        }

        private static ulong DeriveConfirmedRound(string deploymentId)
        {
            var input = $"round:{deploymentId}";
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return BitConverter.ToUInt64(bytes, 0) % 50_000_000UL + 30_000_000UL;
        }
    }
}

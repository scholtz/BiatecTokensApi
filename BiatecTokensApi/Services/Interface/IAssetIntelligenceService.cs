using BiatecTokensApi.Models.AssetIntelligence;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service for asset metadata normalization, provenance scoring, and schema validation.
    /// </summary>
    public interface IAssetIntelligenceService
    {
        /// <summary>
        /// Returns canonical asset metadata with validation status and confidence indicators.
        /// </summary>
        Task<AssetIntelligenceResponse> GetAssetIntelligenceAsync(AssetIntelligenceRequest request);

        /// <summary>
        /// Returns asset quality indicators for a specific asset.
        /// </summary>
        Task<AssetQualityIndicators> GetQualityIndicatorsAsync(ulong assetId, string network);

        /// <summary>
        /// Validates metadata fields against canonical schema.
        /// Returns validation details without persisting changes.
        /// </summary>
        Task<List<AssetValidationDetail>> ValidateMetadataAsync(ulong assetId, string network, IReadOnlyDictionary<string, object?> fields);
    }
}

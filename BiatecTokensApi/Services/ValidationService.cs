using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services.Interface;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Service for validating token metadata and managing validation evidence
    /// </summary>
    public class ValidationService : IValidationService
    {
        private readonly IComplianceRepository _repository;
        private readonly ILogger<ValidationService> _logger;
        private readonly Dictionary<string, ITokenValidator> _validators;

        /// <summary>
        /// Current validator version
        /// </summary>
        private const string ValidatorVersion = "1.0.0";

        /// <summary>
        /// Current rule set version
        /// </summary>
        private const string RuleSetVersion = "1.0.0";

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationService"/> class.
        /// </summary>
        /// <param name="repository">The compliance repository</param>
        /// <param name="logger">The logger instance</param>
        public ValidationService(
            IComplianceRepository repository,
            ILogger<ValidationService> logger)
        {
            _repository = repository;
            _logger = logger;

            // Initialize validators for each token standard
            _validators = new Dictionary<string, ITokenValidator>(StringComparer.OrdinalIgnoreCase)
            {
                { "ASA", new AsaValidator() },
                { "ARC3", new Arc3Validator() },
                { "ARC200", new Arc200Validator() },
                { "ERC20", new Erc20Validator() }
            };
        }

        /// <inheritdoc/>
        public async Task<ValidateTokenMetadataResponse> ValidateTokenMetadataAsync(
            ValidateTokenMetadataRequest request, 
            string requestedBy)
        {
            try
            {
                // Set default versions if not provided
                if (string.IsNullOrEmpty(request.Context.ValidatorVersion))
                {
                    request.Context.ValidatorVersion = ValidatorVersion;
                }
                if (string.IsNullOrEmpty(request.Context.RuleSetVersion))
                {
                    request.Context.RuleSetVersion = RuleSetVersion;
                }

                // Get the appropriate validator
                if (!_validators.TryGetValue(request.Context.TokenStandard, out var validator))
                {
                    return new ValidateTokenMetadataResponse
                    {
                        Success = false,
                        ErrorMessage = $"No validator found for token standard: {request.Context.TokenStandard}"
                    };
                }

                // Perform validation
                var ruleEvaluations = validator.Validate(request.TokenMetadata, request.Context);

                // Calculate statistics
                int totalRules = ruleEvaluations.Count;
                int passedRules = ruleEvaluations.Count(r => r.Passed && !r.Skipped);
                int failedRules = ruleEvaluations.Count(r => !r.Passed && !r.Skipped);
                int skippedRules = ruleEvaluations.Count(r => r.Skipped);

                bool overallPassed = failedRules == 0;

                // Create evidence
                var evidence = new ValidationEvidence
                {
                    EvidenceId = Guid.NewGuid().ToString(),
                    TokenId = null, // Pre-issuance
                    PreIssuanceId = request.PreIssuanceId,
                    Context = request.Context,
                    Passed = overallPassed,
                    RuleEvaluations = ruleEvaluations,
                    ValidationTimestamp = DateTime.UtcNow,
                    RequestedBy = requestedBy,
                    IsDryRun = request.DryRun,
                    TotalRules = totalRules,
                    PassedRules = passedRules,
                    FailedRules = failedRules,
                    SkippedRules = skippedRules,
                    Summary = GenerateSummary(overallPassed, totalRules, passedRules, failedRules, skippedRules)
                };

                // Compute checksum
                evidence.Checksum = ComputeEvidenceChecksum(evidence);

                // Store evidence if not dry-run
                if (!request.DryRun)
                {
                    await _repository.StoreValidationEvidenceAsync(evidence);
                    _logger.LogInformation(
                        "Stored validation evidence {EvidenceId} for {TokenStandard} on {Network}. Result: {Passed}",
                        evidence.EvidenceId,
                        request.Context.TokenStandard,
                        request.Context.Network,
                        overallPassed);
                }
                else
                {
                    _logger.LogInformation(
                        "Dry-run validation for {TokenStandard} on {Network}. Result: {Passed}",
                        request.Context.TokenStandard,
                        request.Context.Network,
                        overallPassed);
                }

                return new ValidateTokenMetadataResponse
                {
                    Success = true,
                    Passed = overallPassed,
                    Evidence = evidence,
                    EvidenceId = request.DryRun ? null : evidence.EvidenceId
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating token metadata");
                return new ValidateTokenMetadataResponse
                {
                    Success = false,
                    ErrorMessage = $"Validation error: {ex.Message}"
                };
            }
        }

        /// <inheritdoc/>
        public async Task<GetValidationEvidenceResponse> GetValidationEvidenceAsync(string evidenceId)
        {
            try
            {
                var evidence = await _repository.GetValidationEvidenceByIdAsync(evidenceId);

                if (evidence == null)
                {
                    return new GetValidationEvidenceResponse
                    {
                        Success = false,
                        ErrorMessage = $"Validation evidence not found: {evidenceId}"
                    };
                }

                return new GetValidationEvidenceResponse
                {
                    Success = true,
                    Evidence = evidence
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving validation evidence {EvidenceId}", evidenceId);
                return new GetValidationEvidenceResponse
                {
                    Success = false,
                    ErrorMessage = $"Error retrieving evidence: {ex.Message}"
                };
            }
        }

        /// <inheritdoc/>
        public async Task<ListValidationEvidenceResponse> ListValidationEvidenceAsync(
            ListValidationEvidenceRequest request)
        {
            try
            {
                var evidence = await _repository.ListValidationEvidenceAsync(request);
                var totalCount = await _repository.GetValidationEvidenceCountAsync(request);
                var totalPages = (int)Math.Ceiling((double)totalCount / request.PageSize);

                return new ListValidationEvidenceResponse
                {
                    Success = true,
                    Evidence = evidence,
                    TotalCount = totalCount,
                    Page = request.Page,
                    PageSize = request.PageSize,
                    TotalPages = totalPages
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing validation evidence");
                return new ListValidationEvidenceResponse
                {
                    Success = false,
                    ErrorMessage = $"Error listing evidence: {ex.Message}"
                };
            }
        }

        /// <inheritdoc/>
        public async Task<bool> VerifyTokenHasPassingValidationAsync(ulong? tokenId, string? preIssuanceId)
        {
            try
            {
                var evidence = await _repository.GetMostRecentPassingValidationAsync(tokenId, preIssuanceId);
                return evidence != null && evidence.Passed;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying token validation status");
                return false;
            }
        }

        /// <inheritdoc/>
        public string ComputeEvidenceChecksum(ValidationEvidence evidence)
        {
            // Create a deterministic JSON representation for checksum
            var checksumData = new
            {
                evidence.EvidenceId,
                evidence.TokenId,
                evidence.PreIssuanceId,
                evidence.Context.Network,
                evidence.Context.TokenStandard,
                evidence.Context.IssuerRole,
                evidence.Context.ValidatorVersion,
                evidence.Context.RuleSetVersion,
                evidence.Passed,
                evidence.ValidationTimestamp,
                evidence.RequestedBy,
                evidence.TotalRules,
                evidence.PassedRules,
                evidence.FailedRules,
                evidence.SkippedRules,
                RuleEvaluations = evidence.RuleEvaluations.Select(r => new
                {
                    r.RuleId,
                    r.Passed,
                    r.Skipped,
                    r.ErrorMessage
                }).OrderBy(r => r.RuleId).ToList()
            };

            var json = JsonSerializer.Serialize(checksumData);
            var bytes = Encoding.UTF8.GetBytes(json);
            var hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        /// <summary>
        /// Generates a summary of validation results
        /// </summary>
        private string GenerateSummary(bool passed, int total, int passedCount, int failed, int skipped)
        {
            if (passed)
            {
                return $"Validation passed: {passedCount}/{total} rules passed" +
                    (skipped > 0 ? $", {skipped} rules skipped" : "");
            }
            else
            {
                return $"Validation failed: {failed} rule(s) failed, {passedCount} passed" +
                    (skipped > 0 ? $", {skipped} skipped" : "");
            }
        }
    }

    /// <summary>
    /// Interface for token-standard-specific validators
    /// </summary>
    internal interface ITokenValidator
    {
        /// <summary>
        /// Validates token metadata against rules
        /// </summary>
        /// <param name="metadata">The token metadata</param>
        /// <param name="context">The validation context</param>
        /// <returns>List of rule evaluations</returns>
        List<RuleEvaluation> Validate(object metadata, ValidationContext context);
    }
}

using System.Numerics;
using System.Text.Json;
using BiatecTokensApi.Services.Interface;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Service for validating and normalizing token metadata across different standards
    /// </summary>
    public class TokenMetadataValidator : ITokenMetadataValidator
    {
        private readonly ILogger<TokenMetadataValidator> _logger;

        public TokenMetadataValidator(ILogger<TokenMetadataValidator> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc/>
        public TokenMetadataValidationResult ValidateARC3Metadata(object metadata)
        {
            var result = new TokenMetadataValidationResult();

            try
            {
                if (metadata == null)
                {
                    result.Errors.Add(new TokenValidationIssue
                    {
                        Field = "metadata",
                        Message = "Metadata cannot be null",
                        Severity = "Error"
                    });
                    return result;
                }

                // Convert to dictionary if it isn't already
                Dictionary<string, object>? arc3Data;
                if (metadata is Dictionary<string, object> dict)
                {
                    arc3Data = dict;
                }
                else
                {
                    var json = JsonSerializer.Serialize(metadata);
                    arc3Data = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                }

                if (arc3Data == null)
                {
                    result.Errors.Add(new TokenValidationIssue
                    {
                        Field = "metadata",
                        Message = "Metadata cannot be null",
                        Severity = "Error"
                    });
                    return result;
                }

                // Validate required ARC3 fields
                ValidateRequiredField(arc3Data, "name", result);
                ValidateRequiredField(arc3Data, "decimals", result);

                // Validate optional but recommended fields
                ValidateOptionalField(arc3Data, "symbol", result);
                ValidateOptionalField(arc3Data, "description", result);
                ValidateOptionalField(arc3Data, "image", result);

                // Validate decimals range
                if (arc3Data.ContainsKey("decimals") && arc3Data["decimals"] != null)
                {
                    var decimals = Convert.ToInt32(arc3Data["decimals"]);
                    if (decimals < 0 || decimals > 19)
                    {
                        result.Errors.Add(new TokenValidationIssue
                        {
                            Field = "decimals",
                            Message = "Decimals must be between 0 and 19",
                            Severity = "Error",
                            ExpectedFormat = "0-19",
                            ActualValue = decimals.ToString()
                        });
                    }
                }

                result.IsValid = result.Errors.Count == 0;
                result.Summary = result.IsValid
                    ? "ARC3 metadata is valid"
                    : $"ARC3 metadata has {result.Errors.Count} error(s) and {result.Warnings.Count} warning(s)";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating ARC3 metadata");
                result.Errors.Add(new TokenValidationIssue
                {
                    Field = "metadata",
                    Message = $"Validation failed: {ex.Message}",
                    Severity = "Error"
                });
            }

            return result;
        }

        /// <inheritdoc/>
        public TokenMetadataValidationResult ValidateARC200Metadata(object metadata)
        {
            var result = new TokenMetadataValidationResult();

            try
            {
                if (metadata == null)
                {
                    result.Errors.Add(new TokenValidationIssue
                    {
                        Field = "metadata",
                        Message = "Metadata cannot be null",
                        Severity = "Error"
                    });
                    return result;
                }

                // Convert to dictionary if it isn't already
                Dictionary<string, object>? arc200Data;
                if (metadata is Dictionary<string, object> dict)
                {
                    arc200Data = dict;
                }
                else
                {
                    var json = JsonSerializer.Serialize(metadata);
                    arc200Data = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                }

                if (arc200Data == null)
                {
                    result.Errors.Add(new TokenValidationIssue
                    {
                        Field = "metadata",
                        Message = "Metadata cannot be null",
                        Severity = "Error"
                    });
                    return result;
                }

                // Validate required ARC200 fields
                ValidateRequiredField(arc200Data, "name", result);
                ValidateRequiredField(arc200Data, "symbol", result);
                ValidateRequiredField(arc200Data, "decimals", result);

                // Validate decimals range for ARC200
                if (arc200Data.ContainsKey("decimals") && arc200Data["decimals"] != null)
                {
                    var decimals = Convert.ToInt32(arc200Data["decimals"]);
                    if (decimals < 0 || decimals > 18)
                    {
                        result.Errors.Add(new TokenValidationIssue
                        {
                            Field = "decimals",
                            Message = "ARC200 decimals must be between 0 and 18",
                            Severity = "Error",
                            ExpectedFormat = "0-18",
                            ActualValue = decimals.ToString()
                        });
                    }
                }

                result.IsValid = result.Errors.Count == 0;
                result.Summary = result.IsValid
                    ? "ARC200 metadata is valid"
                    : $"ARC200 metadata has {result.Errors.Count} error(s) and {result.Warnings.Count} warning(s)";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating ARC200 metadata");
                result.Errors.Add(new TokenValidationIssue
                {
                    Field = "metadata",
                    Message = $"Validation failed: {ex.Message}",
                    Severity = "Error"
                });
            }

            return result;
        }

        /// <inheritdoc/>
        public TokenMetadataValidationResult ValidateERC20Metadata(object metadata)
        {
            var result = new TokenMetadataValidationResult();

            try
            {
                if (metadata == null)
                {
                    result.Errors.Add(new TokenValidationIssue
                    {
                        Field = "metadata",
                        Message = "Metadata cannot be null",
                        Severity = "Error"
                    });
                    return result;
                }

                // Convert to dictionary if it isn't already
                Dictionary<string, object>? erc20Data;
                if (metadata is Dictionary<string, object> dict)
                {
                    erc20Data = dict;
                }
                else
                {
                    var json = JsonSerializer.Serialize(metadata);
                    erc20Data = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                }

                if (erc20Data == null)
                {
                    result.Errors.Add(new TokenValidationIssue
                    {
                        Field = "metadata",
                        Message = "Metadata cannot be null",
                        Severity = "Error"
                    });
                    return result;
                }

                // Validate required ERC20 fields
                ValidateRequiredField(erc20Data, "name", result);
                ValidateRequiredField(erc20Data, "symbol", result);
                ValidateRequiredField(erc20Data, "decimals", result);

                // Validate decimals (ERC20 standard typically uses 18)
                if (erc20Data.ContainsKey("decimals") && erc20Data["decimals"] != null)
                {
                    var decimals = Convert.ToInt32(erc20Data["decimals"]);
                    if (decimals < 0 || decimals > 18)
                    {
                        result.Errors.Add(new TokenValidationIssue
                        {
                            Field = "decimals",
                            Message = "ERC20 decimals must be between 0 and 18",
                            Severity = "Error",
                            ExpectedFormat = "0-18",
                            ActualValue = decimals.ToString()
                        });
                    }
                }

                result.IsValid = result.Errors.Count == 0;
                result.Summary = result.IsValid
                    ? "ERC20 metadata is valid"
                    : $"ERC20 metadata has {result.Errors.Count} error(s) and {result.Warnings.Count} warning(s)";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating ERC20 metadata");
                result.Errors.Add(new TokenValidationIssue
                {
                    Field = "metadata",
                    Message = $"Validation failed: {ex.Message}",
                    Severity = "Error"
                });
            }

            return result;
        }

        /// <inheritdoc/>
        public TokenMetadataValidationResult ValidateERC721Metadata(object metadata)
        {
            var result = new TokenMetadataValidationResult();

            try
            {
                if (metadata == null)
                {
                    result.Errors.Add(new TokenValidationIssue
                    {
                        Field = "metadata",
                        Message = "Metadata cannot be null",
                        Severity = "Error"
                    });
                    return result;
                }

                // Convert to dictionary if it isn't already
                Dictionary<string, object>? erc721Data;
                if (metadata is Dictionary<string, object> dict)
                {
                    erc721Data = dict;
                }
                else
                {
                    var json = JsonSerializer.Serialize(metadata);
                    erc721Data = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                }

                if (erc721Data == null)
                {
                    result.Errors.Add(new TokenValidationIssue
                    {
                        Field = "metadata",
                        Message = "Metadata cannot be null",
                        Severity = "Error"
                    });
                    return result;
                }

                // Validate required ERC721 fields
                ValidateRequiredField(erc721Data, "name", result);

                // Validate optional but recommended fields for NFTs
                ValidateOptionalField(erc721Data, "description", result);
                ValidateOptionalField(erc721Data, "image", result);

                result.IsValid = result.Errors.Count == 0;
                result.Summary = result.IsValid
                    ? "ERC721 metadata is valid"
                    : $"ERC721 metadata has {result.Errors.Count} error(s) and {result.Warnings.Count} warning(s)";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating ERC721 metadata");
                result.Errors.Add(new TokenValidationIssue
                {
                    Field = "metadata",
                    Message = $"Validation failed: {ex.Message}",
                    Severity = "Error"
                });
            }

            return result;
        }

        /// <inheritdoc/>
        public NormalizedMetadata NormalizeMetadata(object metadata, string standard)
        {
            var normalized = new NormalizedMetadata
            {
                Standard = standard
            };

            try
            {
                var json = JsonSerializer.Serialize(metadata);
                var data = JsonSerializer.Deserialize<Dictionary<string, object>>(json) ?? new Dictionary<string, object>();

                // Apply standard-specific defaults
                switch (standard.ToUpperInvariant())
                {
                    case "ARC3":
                    case "ARC200":
                    case "ERC20":
                        ApplyFungibleTokenDefaults(data, normalized);
                        break;
                    case "ERC721":
                    case "NFT":
                        ApplyNFTDefaults(data, normalized);
                        break;
                    default:
                        ApplyGenericDefaults(data, normalized);
                        break;
                }

                normalized.Metadata = data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error normalizing metadata for standard {Standard}", standard);
                normalized.WarningSignals.Add($"Metadata normalization failed: {ex.Message}");
            }

            return normalized;
        }

        /// <inheritdoc/>
        public DecimalValidationResult ValidateDecimalPrecision(decimal amount, int decimals)
        {
            var result = new DecimalValidationResult
            {
                MaxPrecision = decimals,
                IsValid = true
            };

            try
            {
                // Convert to string to check actual decimal places
                var amountStr = amount.ToString("F18");
                var decimalIndex = amountStr.IndexOf('.');
                
                if (decimalIndex >= 0)
                {
                    var fractionalPart = amountStr.Substring(decimalIndex + 1).TrimEnd('0');
                    result.ActualPrecision = fractionalPart.Length;

                    if (result.ActualPrecision > decimals)
                    {
                        result.IsValid = false;
                        result.HasPrecisionLoss = true;
                        result.ErrorMessage = $"Amount has {result.ActualPrecision} decimal places but token supports only {decimals}";
                        
                        // Calculate recommended value with proper precision
                        var multiplier = (decimal)Math.Pow(10, decimals);
                        result.RecommendedValue = Math.Floor(amount * multiplier) / multiplier;
                    }
                }
                else
                {
                    result.ActualPrecision = 0;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating decimal precision for amount {Amount} with {Decimals} decimals", amount, decimals);
                result.IsValid = false;
                result.ErrorMessage = $"Decimal validation failed: {ex.Message}";
            }

            return result;
        }

        /// <inheritdoc/>
        public decimal ConvertRawToDisplayBalance(string rawBalance, int decimals)
        {
            try
            {
                if (!BigInteger.TryParse(rawBalance, out var rawValue))
                {
                    _logger.LogWarning("Invalid raw balance format: {RawBalance}", rawBalance);
                    return 0;
                }

                var divisor = BigInteger.Pow(10, decimals);
                var wholePart = rawValue / divisor;
                var fractionalPart = rawValue % divisor;

                // Convert to decimal carefully to avoid overflow
                var result = (decimal)wholePart;
                if (fractionalPart > 0)
                {
                    var fractionalDecimal = (decimal)fractionalPart / (decimal)divisor;
                    result += fractionalDecimal;
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting raw balance {RawBalance} to display balance with {Decimals} decimals", rawBalance, decimals);
                return 0;
            }
        }

        /// <inheritdoc/>
        public string ConvertDisplayToRawBalance(decimal displayBalance, int decimals)
        {
            try
            {
                var multiplier = BigInteger.Pow(10, decimals);
                var displayAsDecimal = displayBalance;
                
                // Multiply by 10^decimals to get raw value
                var rawValue = (BigInteger)(displayAsDecimal * (decimal)multiplier);
                
                return rawValue.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting display balance {DisplayBalance} to raw balance with {Decimals} decimals", displayBalance, decimals);
                return "0";
            }
        }

        private void ValidateRequiredField(Dictionary<string, object> data, string fieldName, TokenMetadataValidationResult result)
        {
            if (!data.ContainsKey(fieldName) || data[fieldName] == null || string.IsNullOrWhiteSpace(data[fieldName].ToString()))
            {
                result.Errors.Add(new TokenValidationIssue
                {
                    Field = fieldName,
                    Message = $"Required field '{fieldName}' is missing or empty",
                    Severity = "Error",
                    SuggestedFix = $"Provide a value for '{fieldName}'"
                });
            }
        }

        private void ValidateOptionalField(Dictionary<string, object> data, string fieldName, TokenMetadataValidationResult result)
        {
            if (!data.ContainsKey(fieldName) || data[fieldName] == null || string.IsNullOrWhiteSpace(data[fieldName].ToString()))
            {
                result.Warnings.Add(new TokenValidationIssue
                {
                    Field = fieldName,
                    Message = $"Recommended field '{fieldName}' is missing",
                    Severity = "Warning",
                    SuggestedFix = $"Consider adding '{fieldName}' for better user experience"
                });
            }
        }

        private void ApplyFungibleTokenDefaults(Dictionary<string, object> data, NormalizedMetadata normalized)
        {
            if (!data.ContainsKey("name") || string.IsNullOrWhiteSpace(data["name"].ToString()))
            {
                data["name"] = "Unknown Token";
                normalized.DefaultedFields.Add("name");
                normalized.HasDefaults = true;
                normalized.WarningSignals.Add("Token name is missing - displaying as 'Unknown Token'");
            }

            if (!data.ContainsKey("symbol") || string.IsNullOrWhiteSpace(data["symbol"].ToString()))
            {
                data["symbol"] = "???";
                normalized.DefaultedFields.Add("symbol");
                normalized.HasDefaults = true;
                normalized.WarningSignals.Add("Token symbol is missing - displaying as '???'");
            }

            if (!data.ContainsKey("decimals"))
            {
                data["decimals"] = 0;
                normalized.DefaultedFields.Add("decimals");
                normalized.HasDefaults = true;
                normalized.WarningSignals.Add("Token decimals not specified - using 0");
            }

            if (!data.ContainsKey("description"))
            {
                data["description"] = "No description available";
                normalized.DefaultedFields.Add("description");
                normalized.HasDefaults = true;
            }
        }

        private void ApplyNFTDefaults(Dictionary<string, object> data, NormalizedMetadata normalized)
        {
            if (!data.ContainsKey("name") || string.IsNullOrWhiteSpace(data["name"].ToString()))
            {
                data["name"] = "Unnamed NFT";
                normalized.DefaultedFields.Add("name");
                normalized.HasDefaults = true;
                normalized.WarningSignals.Add("NFT name is missing - displaying as 'Unnamed NFT'");
            }

            if (!data.ContainsKey("description"))
            {
                data["description"] = "No description available";
                normalized.DefaultedFields.Add("description");
                normalized.HasDefaults = true;
            }

            if (!data.ContainsKey("image"))
            {
                normalized.WarningSignals.Add("NFT image is missing - may not display properly");
            }
        }

        private void ApplyGenericDefaults(Dictionary<string, object> data, NormalizedMetadata normalized)
        {
            if (!data.ContainsKey("name") || string.IsNullOrWhiteSpace(data["name"].ToString()))
            {
                data["name"] = "Unknown Token";
                normalized.DefaultedFields.Add("name");
                normalized.HasDefaults = true;
            }
        }
    }
}

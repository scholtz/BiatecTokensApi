using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.TokenStandards;
using BiatecTokensApi.Services.Interface;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Service for validating token metadata against standards
    /// </summary>
    public class TokenStandardValidator : ITokenStandardValidator
    {
        private readonly ILogger<TokenStandardValidator> _logger;
        private readonly ITokenStandardRegistry _registry;

        /// <summary>
        /// Initializes a new instance of the TokenStandardValidator
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="registry">Token standard registry</param>
        public TokenStandardValidator(
            ILogger<TokenStandardValidator> logger,
            ITokenStandardRegistry registry)
        {
            _logger = logger;
            _registry = registry;
        }

        /// <summary>
        /// Validates token metadata against a specified standard profile
        /// </summary>
        public async Task<TokenValidationResult> ValidateAsync(
            TokenStandard standard,
            object? metadata,
            string? tokenName = null,
            string? tokenSymbol = null,
            int? decimals = null)
        {
            var result = new TokenValidationResult
            {
                Standard = standard,
                ValidatedAt = DateTime.UtcNow
            };

            try
            {
                // Get the standard profile
                var profile = await _registry.GetStandardProfileAsync(standard);
                if (profile == null)
                {
                    result.IsValid = false;
                    result.Errors.Add(new ValidationError
                    {
                        Code = ErrorCodes.INVALID_TOKEN_STANDARD,
                        Field = "standard",
                        Message = $"Token standard '{standard}' is not supported",
                        Severity = TokenValidationSeverity.Error
                    });
                    return result;
                }

                result.StandardVersion = profile.Version;

                // Convert metadata to dictionary for easier validation
                var metadataDict = ConvertToDictionary(metadata);

                // Add context fields if provided
                if (!string.IsNullOrEmpty(tokenName))
                {
                    metadataDict["name"] = tokenName;
                }
                if (!string.IsNullOrEmpty(tokenSymbol))
                {
                    metadataDict["symbol"] = tokenSymbol;
                }
                if (decimals.HasValue)
                {
                    metadataDict["decimals"] = decimals.Value;
                }

                // Validate required fields
                var requiredFieldErrors = await ValidateRequiredFieldsInternal(profile, metadataDict);
                result.Errors.AddRange(requiredFieldErrors);

                // Validate field types and constraints
                var fieldTypeErrors = await ValidateFieldTypesInternal(profile, metadataDict);
                result.Errors.AddRange(fieldTypeErrors);

                // Apply custom validation rules
                var ruleErrors = await ValidateCustomRulesAsync(profile, metadataDict);
                result.Errors.AddRange(ruleErrors.Where(e => e.Severity == TokenValidationSeverity.Error));
                result.Warnings.AddRange(ruleErrors.Where(e => e.Severity == TokenValidationSeverity.Warning));

                // Set overall validity
                result.IsValid = result.Errors.Count == 0;
                result.Message = result.IsValid
                    ? (result.Warnings.Count > 0 
                        ? $"Validation passed with {result.Warnings.Count} warning(s)" 
                        : "Validation passed successfully")
                    : $"Validation failed with {result.Errors.Count} error(s)";

                _logger.LogInformation(
                    "Token metadata validation completed for standard {Standard}: IsValid={IsValid}, Errors={ErrorCount}, Warnings={WarningCount}",
                    LoggingHelper.SanitizeLogInput(standard.ToString()),
                    result.IsValid,
                    result.Errors.Count,
                    result.Warnings.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating token metadata for standard {Standard}", 
                    LoggingHelper.SanitizeLogInput(standard.ToString()));
                result.IsValid = false;
                result.Errors.Add(new ValidationError
                {
                    Code = ErrorCodes.UNEXPECTED_ERROR,
                    Field = "metadata",
                    Message = "An unexpected error occurred during validation",
                    Severity = TokenValidationSeverity.Error
                });
            }

            return result;
        }

        /// <summary>
        /// Validates that required fields are present
        /// </summary>
        public async Task<List<ValidationError>> ValidateRequiredFieldsAsync(
            TokenStandardProfile profile,
            object? metadata)
        {
            var metadataDict = ConvertToDictionary(metadata);
            return await ValidateRequiredFieldsInternal(profile, metadataDict);
        }

        /// <summary>
        /// Validates field types and constraints
        /// </summary>
        public async Task<List<ValidationError>> ValidateFieldTypesAsync(
            TokenStandardProfile profile,
            object? metadata)
        {
            var metadataDict = ConvertToDictionary(metadata);
            return await ValidateFieldTypesInternal(profile, metadataDict);
        }

        /// <summary>
        /// Checks if the validator supports a given standard
        /// </summary>
        public bool SupportsStandard(TokenStandard standard)
        {
            return true; // This validator supports all standards through the registry
        }

        /// <summary>
        /// Internal method to validate required fields
        /// </summary>
        private Task<List<ValidationError>> ValidateRequiredFieldsInternal(
            TokenStandardProfile profile,
            Dictionary<string, object> metadataDict)
        {
            var errors = new List<ValidationError>();

            foreach (var field in profile.RequiredFields)
            {
                if (!metadataDict.ContainsKey(field.Name) || metadataDict[field.Name] == null)
                {
                    errors.Add(new ValidationError
                    {
                        Code = ErrorCodes.REQUIRED_METADATA_FIELD_MISSING,
                        Field = field.Name,
                        Message = $"Required field '{field.Name}' is missing",
                        Severity = TokenValidationSeverity.Error,
                        Details = field.Description
                    });
                }
            }

            return Task.FromResult(errors);
        }

        /// <summary>
        /// Internal method to validate field types and constraints
        /// </summary>
        private Task<List<ValidationError>> ValidateFieldTypesInternal(
            TokenStandardProfile profile,
            Dictionary<string, object> metadataDict)
        {
            var errors = new List<ValidationError>();
            var allFields = profile.RequiredFields.Concat(profile.OptionalFields).ToList();

            foreach (var field in allFields)
            {
                if (!metadataDict.ContainsKey(field.Name) || metadataDict[field.Name] == null)
                {
                    continue; // Skip validation for missing optional fields
                }

                var value = metadataDict[field.Name];
                var fieldErrors = ValidateFieldValue(field, value);
                errors.AddRange(fieldErrors);
            }

            return Task.FromResult(errors);
        }

        /// <summary>
        /// Validates a single field value against its definition
        /// </summary>
        private List<ValidationError> ValidateFieldValue(StandardFieldDefinition field, object value)
        {
            var errors = new List<ValidationError>();

            // Type validation
            var expectedType = field.DataType.ToLowerInvariant();
            var actualType = GetValueType(value);

            if (!IsTypeCompatible(expectedType, actualType, value))
            {
                errors.Add(new ValidationError
                {
                    Code = ErrorCodes.METADATA_FIELD_TYPE_MISMATCH,
                    Field = field.Name,
                    Message = $"Field '{field.Name}' expects type '{field.DataType}' but got '{actualType}'",
                    Severity = TokenValidationSeverity.Error
                });
                return errors; // Skip further validation if type is wrong
            }

            // String-specific validation
            if (expectedType == "string" && value is string strValue)
            {
                if (field.MaxLength.HasValue && strValue.Length > field.MaxLength.Value)
                {
                    errors.Add(new ValidationError
                    {
                        Code = ErrorCodes.METADATA_FIELD_VALIDATION_FAILED,
                        Field = field.Name,
                        Message = $"Field '{field.Name}' exceeds maximum length of {field.MaxLength.Value}",
                        Severity = TokenValidationSeverity.Error
                    });
                }

                if (!string.IsNullOrEmpty(field.ValidationPattern))
                {
                    try
                    {
                        if (!Regex.IsMatch(strValue, field.ValidationPattern))
                        {
                            errors.Add(new ValidationError
                            {
                                Code = ErrorCodes.METADATA_FIELD_VALIDATION_FAILED,
                                Field = field.Name,
                                Message = $"Field '{field.Name}' does not match required pattern",
                                Severity = TokenValidationSeverity.Error,
                                Details = $"Expected pattern: {field.ValidationPattern}"
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Invalid regex pattern for field {FieldName}: {Pattern}",
                            LoggingHelper.SanitizeLogInput(field.Name),
                            LoggingHelper.SanitizeLogInput(field.ValidationPattern));
                    }
                }
            }

            // Numeric-specific validation
            if ((expectedType == "number" || expectedType == "integer") && IsNumeric(value))
            {
                var numValue = Convert.ToDouble(value);

                if (field.MinValue.HasValue && numValue < field.MinValue.Value)
                {
                    errors.Add(new ValidationError
                    {
                        Code = ErrorCodes.METADATA_FIELD_VALIDATION_FAILED,
                        Field = field.Name,
                        Message = $"Field '{field.Name}' is below minimum value of {field.MinValue.Value}",
                        Severity = TokenValidationSeverity.Error
                    });
                }

                if (field.MaxValue.HasValue && numValue > field.MaxValue.Value)
                {
                    errors.Add(new ValidationError
                    {
                        Code = ErrorCodes.METADATA_FIELD_VALIDATION_FAILED,
                        Field = field.Name,
                        Message = $"Field '{field.Name}' exceeds maximum value of {field.MaxValue.Value}",
                        Severity = TokenValidationSeverity.Error
                    });
                }
            }

            return errors;
        }

        /// <summary>
        /// Validates custom rules from the standard profile
        /// </summary>
        private Task<List<ValidationError>> ValidateCustomRulesAsync(
            TokenStandardProfile profile,
            Dictionary<string, object> metadataDict)
        {
            var errors = new List<ValidationError>();

            foreach (var rule in profile.ValidationRules)
            {
                var ruleError = ApplyCustomRule(rule, profile, metadataDict);
                if (ruleError != null)
                {
                    errors.Add(ruleError);
                }
            }

            return Task.FromResult(errors);
        }

        /// <summary>
        /// Applies a custom validation rule
        /// </summary>
        private ValidationError? ApplyCustomRule(
            ValidationRule rule,
            TokenStandardProfile profile,
            Dictionary<string, object> metadataDict)
        {
            // ARC-3 specific rules
            if (profile.Standard == TokenStandard.ARC3)
            {
                if (rule.Id == "arc3-image-mimetype")
                {
                    if (metadataDict.ContainsKey("image") && metadataDict.ContainsKey("image_mimetype"))
                    {
                        var mimetype = metadataDict["image_mimetype"]?.ToString();
                        if (!string.IsNullOrEmpty(mimetype) && !mimetype.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                        {
                            return new ValidationError
                            {
                                Code = rule.ErrorCode,
                                Field = "image_mimetype",
                                Message = rule.ErrorMessage,
                                Severity = rule.Severity
                            };
                        }
                    }
                }
                else if (rule.Id == "arc3-background-color")
                {
                    if (metadataDict.ContainsKey("background_color"))
                    {
                        var color = metadataDict["background_color"]?.ToString();
                        if (!string.IsNullOrEmpty(color) && !Regex.IsMatch(color, @"^[0-9A-Fa-f]{6}$"))
                        {
                            return new ValidationError
                            {
                                Code = rule.ErrorCode,
                                Field = "background_color",
                                Message = rule.ErrorMessage,
                                Severity = rule.Severity
                            };
                        }
                    }
                }
            }

            // ARC-19 specific rules
            if (profile.Standard == TokenStandard.ARC19)
            {
                if (rule.Id == "arc19-name-length" && metadataDict.ContainsKey("name"))
                {
                    var name = metadataDict["name"]?.ToString();
                    if (!string.IsNullOrEmpty(name) && name.Length > 32)
                    {
                        return new ValidationError
                        {
                            Code = rule.ErrorCode,
                            Field = "name",
                            Message = rule.ErrorMessage,
                            Severity = rule.Severity
                        };
                    }
                }
                else if (rule.Id == "arc19-unit-name-length" && metadataDict.ContainsKey("unit_name"))
                {
                    var unitName = metadataDict["unit_name"]?.ToString();
                    if (!string.IsNullOrEmpty(unitName) && unitName.Length > 8)
                    {
                        return new ValidationError
                        {
                            Code = rule.ErrorCode,
                            Field = "unit_name",
                            Message = rule.ErrorMessage,
                            Severity = rule.Severity
                        };
                    }
                }
            }

            // ARC-69 specific rules
            if (profile.Standard == TokenStandard.ARC69)
            {
                if (rule.Id == "arc69-standard-field" && metadataDict.ContainsKey("standard"))
                {
                    var standardValue = metadataDict["standard"]?.ToString();
                    if (!string.IsNullOrEmpty(standardValue) && !standardValue.Equals("arc69", StringComparison.OrdinalIgnoreCase))
                    {
                        return new ValidationError
                        {
                            Code = rule.ErrorCode,
                            Field = "standard",
                            Message = rule.ErrorMessage,
                            Severity = rule.Severity
                        };
                    }
                }
            }

            // ERC-20 specific rules
            if (profile.Standard == TokenStandard.ERC20)
            {
                if (rule.Id == "erc20-symbol-length" && metadataDict.ContainsKey("symbol"))
                {
                    var symbol = metadataDict["symbol"]?.ToString();
                    if (!string.IsNullOrEmpty(symbol) && symbol.Length > 11)
                    {
                        return new ValidationError
                        {
                            Code = rule.ErrorCode,
                            Field = "symbol",
                            Message = rule.ErrorMessage,
                            Severity = rule.Severity
                        };
                    }
                }
                else if (rule.Id == "erc20-decimals-range" && metadataDict.ContainsKey("decimals"))
                {
                    if (IsNumeric(metadataDict["decimals"]))
                    {
                        var decimals = Convert.ToInt32(metadataDict["decimals"]);
                        if (decimals < 0 || decimals > 18)
                        {
                            return new ValidationError
                            {
                                Code = rule.ErrorCode,
                                Field = "decimals",
                                Message = rule.ErrorMessage,
                                Severity = rule.Severity
                            };
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Converts metadata object to dictionary
        /// </summary>
        private Dictionary<string, object> ConvertToDictionary(object? metadata)
        {
            if (metadata == null)
            {
                return new Dictionary<string, object>();
            }

            if (metadata is Dictionary<string, object> dict)
            {
                return dict;
            }

            if (metadata is IDictionary<string, object> iDict)
            {
                return new Dictionary<string, object>(iDict);
            }

            // Try to convert using JSON serialization
            try
            {
                var json = JsonSerializer.Serialize(metadata);
                var result = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                return result ?? new Dictionary<string, object>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to convert metadata to dictionary");
                return new Dictionary<string, object>();
            }
        }

        /// <summary>
        /// Gets the type name of a value
        /// </summary>
        private string GetValueType(object value)
        {
            if (value == null) return "null";
            if (value is string) return "string";
            if (value is bool) return "boolean";
            if (IsNumeric(value)) return "number";
            if (value is Array || value is System.Collections.IList) return "array";
            if (value is IDictionary<string, object> || value is JsonElement) return "object";
            return value.GetType().Name.ToLowerInvariant();
        }

        /// <summary>
        /// Checks if a value is numeric
        /// </summary>
        private bool IsNumeric(object value)
        {
            return value is byte || value is sbyte ||
                   value is short || value is ushort ||
                   value is int || value is uint ||
                   value is long || value is ulong ||
                   value is float || value is double ||
                   value is decimal;
        }

        /// <summary>
        /// Checks if actual type is compatible with expected type
        /// </summary>
        private bool IsTypeCompatible(string expectedType, string actualType, object value)
        {
            if (expectedType == actualType) return true;

            // Allow integer for number
            if (expectedType == "number" && IsNumeric(value)) return true;
            if (expectedType == "integer" && IsNumeric(value)) return true;

            // Allow JsonElement objects
            if (value is JsonElement jsonElement)
            {
                return expectedType switch
                {
                    "string" => jsonElement.ValueKind == JsonValueKind.String,
                    "number" => jsonElement.ValueKind == JsonValueKind.Number,
                    "integer" => jsonElement.ValueKind == JsonValueKind.Number,
                    "boolean" => jsonElement.ValueKind == JsonValueKind.True || jsonElement.ValueKind == JsonValueKind.False,
                    "array" => jsonElement.ValueKind == JsonValueKind.Array,
                    "object" => jsonElement.ValueKind == JsonValueKind.Object,
                    _ => false
                };
            }

            return false;
        }
    }
}

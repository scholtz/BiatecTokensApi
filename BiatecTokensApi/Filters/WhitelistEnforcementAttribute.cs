using BiatecTokensApi.Models.Whitelist;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace BiatecTokensApi.Filters
{
    /// <summary>
    /// Action filter attribute that enforces whitelist validation for token operations
    /// </summary>
    /// <remarks>
    /// This filter validates that addresses involved in token operations (transfer, mint, burn)
    /// are properly whitelisted before allowing the operation to proceed. It ensures RWA compliance
    /// by blocking non-whitelisted addresses and logging all validation attempts to the audit trail.
    /// 
    /// **Usage**:
    /// Apply this attribute to any controller action that performs token operations:
    /// ```csharp
    /// [WhitelistEnforcement(AssetIdParameter = "assetId", AddressParameters = new[] { "fromAddress", "toAddress" })]
    /// public async Task&lt;IActionResult&gt; Transfer([FromBody] TransferRequest request)
    /// ```
    /// 
    /// **How It Works**:
    /// 1. Extracts asset ID and addresses from request parameters
    /// 2. Validates each address is whitelisted and active for the specified asset
    /// 3. Blocks the request with explicit error if any address is not whitelisted
    /// 4. Logs the enforcement check to the audit trail
    /// 5. Allows the request to proceed only if all addresses are whitelisted
    /// 
    /// **Error Response**:
    /// Returns HTTP 403 Forbidden with detailed error message when whitelist validation fails.
    /// </remarks>
    public class WhitelistEnforcementAttribute : ActionFilterAttribute
    {
        /// <summary>
        /// Name of the parameter containing the asset ID
        /// </summary>
        public string AssetIdParameter { get; set; } = "assetId";

        /// <summary>
        /// Names of parameters containing addresses to validate
        /// </summary>
        public string[] AddressParameters { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Whether to validate the authenticated user's address
        /// </summary>
        public bool ValidateUserAddress { get; set; } = false;

        /// <summary>
        /// Executes before the action method, validating whitelist compliance
        /// </summary>
        /// <param name="context">The action executing context</param>
        /// <param name="next">The next action execution delegate</param>
        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // Get services from DI container
            var whitelistService = context.HttpContext.RequestServices.GetService<IWhitelistService>();
            var logger = context.HttpContext.RequestServices.GetService<ILogger<WhitelistEnforcementAttribute>>();

            if (whitelistService == null || logger == null)
            {
                logger?.LogError("WhitelistService or Logger not available in DI container");
                context.Result = new ObjectResult(new
                {
                    success = false,
                    isAllowed = false,
                    errorMessage = "Whitelist enforcement service not available"
                })
                {
                    StatusCode = StatusCodes.Status500InternalServerError
                };
                return;
            }

            try
            {
                // Extract asset ID
                if (!TryGetAssetId(context, out ulong assetId))
                {
                    logger.LogWarning("Failed to extract asset ID from request parameters");
                    context.Result = new BadRequestObjectResult(new
                    {
                        success = false,
                        isAllowed = false,
                        errorMessage = $"Asset ID parameter '{AssetIdParameter}' not found or invalid"
                    });
                    return;
                }

                // Get authenticated user address
                var userAddress = GetUserAddress(context.HttpContext);
                if (string.IsNullOrEmpty(userAddress))
                {
                    logger.LogWarning("Failed to get user address from authentication context");
                    context.Result = new UnauthorizedObjectResult(new
                    {
                        success = false,
                        isAllowed = false,
                        errorMessage = "User address not found in authentication context"
                    });
                    return;
                }

                // Collect addresses to validate
                var addressesToValidate = new List<string>();
                
                if (ValidateUserAddress)
                {
                    addressesToValidate.Add(userAddress);
                }

                foreach (var paramName in AddressParameters)
                {
                    if (TryGetAddress(context, paramName, out string? address) && !string.IsNullOrEmpty(address))
                    {
                        addressesToValidate.Add(address);
                    }
                }

                if (!addressesToValidate.Any())
                {
                    logger.LogWarning("No addresses found to validate");
                    context.Result = new BadRequestObjectResult(new
                    {
                        success = false,
                        isAllowed = false,
                        errorMessage = "No addresses found for whitelist validation"
                    });
                    return;
                }

                // Validate each address
                foreach (var address in addressesToValidate.Distinct())
                {
                    var validationRequest = new ValidateTransferRequest
                    {
                        AssetId = assetId,
                        FromAddress = address,
                        ToAddress = address // For checking if address itself is whitelisted
                    };

                    var result = await whitelistService.ValidateTransferAsync(validationRequest, userAddress);

                    if (!result.Success || !result.IsAllowed)
                    {
                        var denialReason = result.DenialReason ?? result.ErrorMessage ?? "Address not whitelisted";
                        
                        logger.LogWarning(
                            "Whitelist enforcement blocked operation: AssetId={AssetId}, Address={Address}, Reason={Reason}",
                            assetId, address, denialReason);

                        context.Result = new ObjectResult(new
                        {
                            success = false,
                            isAllowed = false,
                            errorMessage = $"Operation blocked: {denialReason}",
                            address = address,
                            assetId = assetId
                        })
                        {
                            StatusCode = StatusCodes.Status403Forbidden
                        };
                        return;
                    }
                }

                logger.LogInformation(
                    "Whitelist enforcement passed for AssetId={AssetId}, Addresses={Addresses}",
                    assetId, string.Join(", ", addressesToValidate));

                // All addresses validated successfully, proceed with action
                await next();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Exception during whitelist enforcement");
                context.Result = new ObjectResult(new
                {
                    success = false,
                    isAllowed = false,
                    errorMessage = $"Whitelist enforcement error: {ex.Message}"
                })
                {
                    StatusCode = StatusCodes.Status500InternalServerError
                };
            }
        }

        /// <summary>
        /// Tries to extract the asset ID from request parameters
        /// </summary>
        private bool TryGetAssetId(ActionExecutingContext context, out ulong assetId)
        {
            assetId = 0;

            // Try to get from action arguments
            if (context.ActionArguments.TryGetValue(AssetIdParameter, out var assetIdObj))
            {
                if (assetIdObj is ulong ulongValue)
                {
                    assetId = ulongValue;
                    return true;
                }
                if (assetIdObj != null && ulong.TryParse(assetIdObj.ToString(), out ulong parsedValue))
                {
                    assetId = parsedValue;
                    return true;
                }
            }

            // Try to get from request object properties
            foreach (var arg in context.ActionArguments.Values)
            {
                if (arg == null) continue;

                var property = arg.GetType().GetProperty(AssetIdParameter, 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
                
                if (property != null)
                {
                    var value = property.GetValue(arg);
                    if (value is ulong ulongValue)
                    {
                        assetId = ulongValue;
                        return true;
                    }
                    if (value != null && ulong.TryParse(value.ToString(), out ulong parsedValue))
                    {
                        assetId = parsedValue;
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Tries to extract an address from request parameters
        /// </summary>
        private bool TryGetAddress(ActionExecutingContext context, string parameterName, out string? address)
        {
            address = null;

            // Try to get from action arguments
            if (context.ActionArguments.TryGetValue(parameterName, out var addressObj))
            {
                if (addressObj is string strValue)
                {
                    address = strValue;
                    return true;
                }
                if (addressObj != null)
                {
                    address = addressObj.ToString();
                    return true;
                }
            }

            // Try to get from request object properties
            foreach (var arg in context.ActionArguments.Values)
            {
                if (arg == null) continue;

                var property = arg.GetType().GetProperty(parameterName,
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);

                if (property != null)
                {
                    var value = property.GetValue(arg);
                    if (value is string strValue)
                    {
                        address = strValue;
                        return true;
                    }
                    if (value != null)
                    {
                        address = value.ToString();
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the user's Algorand address from the authentication context
        /// </summary>
        private string GetUserAddress(HttpContext context)
        {
            return context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? context.User.FindFirst("sub")?.Value
                ?? string.Empty;
        }
    }
}

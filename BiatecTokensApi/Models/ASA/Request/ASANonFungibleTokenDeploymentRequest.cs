using System.ComponentModel.DataAnnotations;

namespace BiatecTokensApi.Models.ASA.Request
{
    /// <summary>
    /// Decimals is equal to 0 for non-fungible tokens. Total quantity is equal to 1.
    /// </summary>
    /// <remarks>
    /// This model extends the base token deployment request with additional properties specific to fractional non-fungible tokens.
    /// </remarks>
    public class ASANonFungibleTokenDeploymentRequest : ASABaseTokenDeploymentRequest
    {

    }
}

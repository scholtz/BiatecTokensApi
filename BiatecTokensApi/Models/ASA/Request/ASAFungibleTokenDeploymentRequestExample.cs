using Swashbuckle.AspNetCore.SwaggerGen;
using Microsoft.OpenApi.Models;
using System.Text.Json;

namespace BiatecTokensApi.Models.ASA.Request
{
    /// <summary>
    /// Swagger doc
    /// </summary>
    public class ASAFungibleTokenDeploymentRequestExample : ISchemaFilter
    {
        /// <summary>
        /// Swagger doc
        /// </summary>
        public void Apply(OpenApiSchema schema, SchemaFilterContext context)
        {
            if (context.Type == typeof(ASAFungibleTokenDeploymentRequest))
            {
                schema.Example = OpenApiAnyFactory.CreateFromJson(JsonSerializer.Serialize(new ASAFungibleTokenDeploymentRequest
                {
                    Name = "MyToken",
                    UnitName = "MTKN",
                    Url = "https://www.biatec.io",
                    MetadataHash = new byte[32] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32 },
                    DefaultFrozen = false,
                    ManagerAddress = "ALGONAUTSPIUHDCX3SLFXOFDUKOE4VY36XV4JX2JHQTWJNKVBKPEBQACRY",
                    ReserveAddress = "ALGONAUTSPIUHDCX3SLFXOFDUKOE4VY36XV4JX2JHQTWJNKVBKPEBQACRY",
                    FreezeAddress = "",
                    ClawbackAddress = "",
                    Network = "testnet-v1.0",
                    Decimals = 6,
                    TotalSupply = 10000000000
                }));
            }
        }
    }
}
using Swashbuckle.AspNetCore.SwaggerGen;
using Microsoft.OpenApi.Models;
using System.Text.Json;

namespace BiatecTokensApi.Models.ASA.Request
{
    public class ASABaseTokenDeploymentRequestExample : ISchemaFilter
    {
        public void Apply(OpenApiSchema schema, SchemaFilterContext context)
        {
            if (context.Type == typeof(ASABaseTokenDeploymentRequest))
            {
                schema.Example = OpenApiAnyFactory.CreateFromJson(JsonSerializer.Serialize(new ASABaseTokenDeploymentRequest
                {
                    Name = "MyToken",
                    UnitName = "MTKN",
                    Url = "https://example.com/arc3.json",
                    MetadataHash = new byte[32] { 1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24,25,26,27,28,29,30,31,32 },
                    DefaultFrozen = false,
                    ManagerAddress = "ALGOSOMEADDRESS1234567890",
                    ReserveAddress = "ALGOSOMEADDRESS0987654321",
                    FreezeAddress = "ALGOSOMEADDRESS1122334455",
                    ClawbackAddress = "ALGOSOMEADDRESS5566778899",
                    Network = "testnet-v1.0"
                }));
            }
        }
    }
}
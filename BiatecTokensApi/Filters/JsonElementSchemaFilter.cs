using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Text.Json;

namespace BiatecTokensApi.Filters
{
    /// <summary>
    /// Schema filter to handle JsonElement properties in Swagger generation
    /// </summary>
    public class JsonElementSchemaFilter : ISchemaFilter
    {
        public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
        {
            if ((context.Type == typeof(JsonElement) || context.Type == typeof(JsonElement?)) && schema is OpenApiSchema openApiSchema)
            {
                openApiSchema.Type = JsonSchemaType.Object;
                openApiSchema.AdditionalPropertiesAllowed = true;
                openApiSchema.Description = "Dynamic JSON object";
            }
        }
    }
}

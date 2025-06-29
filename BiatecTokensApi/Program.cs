using AlgorandAuthenticationV2;
using BiatecTokensApi.Configuration;
using BiatecTokensApi.Repositories;
using BiatecTokensApi.Services;

namespace BiatecTokensApi
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllers();

            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
                {
                    Title = "Biatec Tokens API",
                    Version = "v1",
                    Description = "API for deploying and managing ERC20 tokens on Base blockchain and ARC3 tokens on Algorand"
                });
            });

            // Configure settings
            builder.Services.Configure<BlockchainConfig>(
                builder.Configuration.GetSection("BlockchainConfig"));
            
            builder.Services.Configure<IPFSConfig>(
                builder.Configuration.GetSection("IPFSConfig"));

            // Register HTTP client for API calls
            builder.Services.AddHttpClient();

            // Register repositories
            builder.Services.AddScoped<IIPFSRepository, IPFSRepository>();

            // Register the token services
            builder.Services.AddScoped<IERC20TokenService, ERC20TokenService>();
            builder.Services.AddScoped<IARC3FungibleTokenService, ARC3FungibleTokenService>();

            var authOptions = builder.Configuration.GetSection("AlgorandAuthentication").Get<AlgorandAuthenticationOptionsV2>();
            if (authOptions == null) throw new Exception("Config for the authentication is missing");
            builder.Services.AddAuthentication(AlgorandAuthenticationHandlerV2.ID).AddAlgorand(a =>
            {
                a.Realm = authOptions.Realm;
                a.CheckExpiration = authOptions.CheckExpiration;
                a.EmptySuccessOnFailure = authOptions.EmptySuccessOnFailure;
                a.AllowedNetworks = authOptions.AllowedNetworks;
                a.Debug = authOptions.Debug;
            });

            var app = builder.Build();

            app.UseSwagger();
            app.UseSwaggerUI();

            app.UseHttpsRedirection();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}

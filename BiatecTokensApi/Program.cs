using AlgorandAuthenticationV2;
using BiatecTokensApi.Configuration;
using BiatecTokensApi.Models;
using BiatecTokensApi.Repositories;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.OpenApi.Models;

namespace BiatecTokensApi
{
    /// <summary>
    /// Configures and runs the web application for the Biatec Tokens API.
    /// </summary>
    /// <remarks>This class sets up the necessary services, middleware, and configurations for the API,
    /// including controllers, Swagger/OpenAPI documentation, authentication, and token services. It is the entry point
    /// of the application and is responsible for building and starting the web host.</remarks>
    public class Program
    {
        /// <summary>
        /// Configures and runs the web application.
        /// </summary>
        /// <remarks>This method sets up the web application by configuring services, middleware, and
        /// endpoints. It initializes Swagger for API documentation, configures authentication using Algorand, and
        /// registers various services and repositories required for the application. The method then builds and runs
        /// the application.</remarks>
        /// <param name="args">The command-line arguments used to configure the application.</param>
        /// <exception cref="Exception"></exception>
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
                    Description = "API for deploying and managing ERC20 tokens on EVM chains ARC3 tokens and ARC200 tokens on Algorand"
                });
                c.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
                {
                    Description = "ARC-0014 Algorand authentication transaction",
                    In = ParameterLocation.Header,
                    Name = "Authorization",
                    Type = SecuritySchemeType.ApiKey,
                });
                c.OperationFilter<Swashbuckle.AspNetCore.Filters.SecurityRequirementsOperationFilter>();
                c.ResolveConflictingActions(apiDescriptions => apiDescriptions.First()); //This line
                var xmlFile = $"doc/documentation.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                c.IncludeXmlComments(xmlPath);
            });

            // Configure settings
            builder.Services.Configure<EVMChains>(
                builder.Configuration.GetSection("EVMChains"));

            builder.Services.Configure<IPFSConfig>(
                builder.Configuration.GetSection("IPFSConfig"));

            builder.Services.Configure<AppConfiguration>(
                builder.Configuration.GetSection("App"));

            // Register HTTP client for API calls
            builder.Services.AddHttpClient();

            // Register repositories
            builder.Services.AddScoped<IIPFSRepository, IPFSRepository>();

            // Register the token services
            builder.Services.AddScoped<IERC20TokenService, ERC20TokenService>();
            builder.Services.AddScoped<IARC3TokenService, ARC3TokenService>();
            builder.Services.AddScoped<IASATokenService, ASATokenService>();
            builder.Services.AddScoped<IARC200TokenService, ARC200TokenService>();

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

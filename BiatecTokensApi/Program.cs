using AlgorandAuthenticationV2;
using BiatecTokensApi.Configuration;
using BiatecTokensApi.Middleware;
using BiatecTokensApi.Models;
using BiatecTokensApi.Repositories;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;

namespace BiatecTokensApi
{
    /// <summary>
    /// Configures and runs the web application for the Biatec Tokens API.
    /// </summary>
    /// <remarks>This class sets up the necessary services, middleware, and configurations for the API,
    /// including controllers, Swagger/OpenAPI documentation, authentication, and token services. It is the entry point
    /// of the application and is responsible for building and starting the web host.</remarks>
    public partial class Program
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
            
            // Register health checks with detailed component monitoring
            builder.Services.AddHealthChecks()
                .AddCheck<BiatecTokensApi.HealthChecks.IPFSHealthCheck>("ipfs", tags: new[] { "ipfs", "external" })
                .AddCheck<BiatecTokensApi.HealthChecks.AlgorandNetworkHealthCheck>("algorand", tags: new[] { "algorand", "blockchain", "external" })
                .AddCheck<BiatecTokensApi.HealthChecks.EVMChainHealthCheck>("evm", tags: new[] { "evm", "blockchain", "external" });
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "Biatec Tokens API",
                    Version = "v1",
                    Description = File.ReadAllText("README.md") +
                        "\n\n## Subscription Metering\n\n" +
                        "This API includes subscription metering for compliance and whitelist operations. " +
                        "Metering events are emitted as structured logs for billing analytics. " +
                        "See SUBSCRIPTION_METERING.md for detailed documentation on the metering schema and integration.",
                });
                c.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
                {
                    Description = "ARC-0014 Algorand authentication transaction",
                    In = ParameterLocation.Header,
                    Name = "Authorization",
                    Type = SecuritySchemeType.ApiKey,
                });

                c.SchemaFilter<BiatecTokensApi.Models.ASA.Request.ASABaseTokenDeploymentRequestExample>();
                c.SchemaFilter<BiatecTokensApi.Models.ASA.Request.ASAFungibleTokenDeploymentRequestExample>();

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

            builder.Services.Configure<AlgorandAuthenticationOptionsV2>(
                builder.Configuration.GetSection("AlgorandAuthentication"));

            builder.Services.Configure<BiatecTokensApi.Configuration.StripeConfig>(
                builder.Configuration.GetSection("StripeConfig"));

            // Register HTTP client for API calls
            builder.Services.AddHttpClient();

            // Register repositories
            builder.Services.AddSingleton<IIPFSRepository, IPFSRepository>();
            builder.Services.AddSingleton<IWhitelistRepository, WhitelistRepository>();
            builder.Services.AddSingleton<IWhitelistRulesRepository, WhitelistRulesRepository>();
            builder.Services.AddSingleton<BiatecTokensApi.Repositories.Interface.IComplianceRepository, BiatecTokensApi.Repositories.ComplianceRepository>();
            builder.Services.AddSingleton<BiatecTokensApi.Repositories.Interface.ITokenIssuanceRepository, BiatecTokensApi.Repositories.TokenIssuanceRepository>();
            builder.Services.AddSingleton<BiatecTokensApi.Repositories.Interface.IEnterpriseAuditRepository, BiatecTokensApi.Repositories.EnterpriseAuditRepository>();
            builder.Services.AddSingleton<BiatecTokensApi.Repositories.Interface.IWebhookRepository, BiatecTokensApi.Repositories.WebhookRepository>();
            builder.Services.AddSingleton<BiatecTokensApi.Repositories.Interface.ISubscriptionRepository, BiatecTokensApi.Repositories.SubscriptionRepository>();

            // Register metering service
            builder.Services.AddSingleton<ISubscriptionMeteringService, SubscriptionMeteringService>();

            // Register subscription tier service
            builder.Services.AddSingleton<ISubscriptionTierService, SubscriptionTierService>();

            // Register billing service
            builder.Services.AddSingleton<IBillingService, BillingService>();

            // Register the token services
            builder.Services.AddSingleton<IERC20TokenService, ERC20TokenService>();
            builder.Services.AddSingleton<IARC3TokenService, ARC3TokenService>();
            builder.Services.AddSingleton<IASATokenService, ASATokenService>();
            builder.Services.AddSingleton<IARC200TokenService, ARC200TokenService>();
            builder.Services.AddSingleton<IARC1400TokenService, ARC1400TokenService>();
            builder.Services.AddSingleton<IWhitelistService, WhitelistService>();
            builder.Services.AddSingleton<IWhitelistRulesService, WhitelistRulesService>();
            builder.Services.AddSingleton<IComplianceService, ComplianceService>();
            builder.Services.AddSingleton<IEnterpriseAuditService, EnterpriseAuditService>();
            builder.Services.AddSingleton<IWebhookService, WebhookService>();
            builder.Services.AddSingleton<IStripeService, StripeService>();

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
            // setup cors
            var corsConfig = builder.Configuration.GetSection("Cors").AsEnumerable().Select(k => k.Value).Where(k => !string.IsNullOrEmpty(k)).ToArray();

            builder.Services.AddCors(options =>
            {
                options.AddPolicy(
                    name: "cors",
                    builder =>
                    {
                        builder.WithOrigins(corsConfig)
                            .AllowAnyMethod()
                            .AllowAnyHeader()
                            .AllowCredentials();
                    });
            });


            var app = builder.Build();

            // Add global exception handler middleware (should be first)
            app.UseGlobalExceptionHandler();
            
            // Add request/response logging middleware for debugging
            app.UseRequestResponseLogging();

            app.UseSwagger();
            app.UseSwaggerUI();
            app.UseCors("cors");
            app.Logger.LogInformation("CORS: " + string.Join(",", corsConfig));

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();
            
            // Map health check endpoints
            // Basic health check for simple monitoring
            app.MapHealthChecks("/health");
            
            // Readiness probe - checks if app is ready to receive traffic
            app.MapHealthChecks("/health/ready");
            
            // Liveness probe - checks if app is running
            app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
            {
                Predicate = _ => false // Don't run any checks, just verify app is responsive
            });

            _ = app.Services.GetService<IARC3TokenService>() ?? throw new Exception("ARC3 Token Service is not registered");
            _ = app.Services.GetService<IARC200TokenService>() ?? throw new Exception("ARC200 Token Service is not registered");
            _ = app.Services.GetService<IARC1400TokenService>() ?? throw new Exception("ARC1400 Token Service is not registered");
            _ = app.Services.GetService<IASATokenService>() ?? throw new Exception("ASA Token Service is not registered");
            _ = app.Services.GetService<IERC20TokenService>() ?? throw new Exception("ERC20 Token Service is not registered");
            _ = app.Services.GetService<IIPFSRepository>() ?? throw new Exception("IPFS Repository is not registered");
            _ = app.Services.GetService<IWhitelistRepository>() ?? throw new Exception("Whitelist Repository is not registered");
            _ = app.Services.GetService<IWhitelistService>() ?? throw new Exception("Whitelist Service is not registered");
            _ = app.Services.GetService<IWhitelistRulesRepository>() ?? throw new Exception("Whitelist Rules Repository is not registered");
            _ = app.Services.GetService<IWhitelistRulesService>() ?? throw new Exception("Whitelist Rules Service is not registered");

            app.Run();
        }
    }
}

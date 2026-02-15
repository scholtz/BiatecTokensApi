using AlgorandAuthenticationV2;
using BiatecTokensApi.Configuration;
using BiatecTokensApi.Middleware;
using BiatecTokensApi.Models;
using BiatecTokensApi.Repositories;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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
            builder.Services.AddHttpContextAccessor(); // For correlation ID access in services
            builder.Services.AddControllers();

            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            
            // Register health checks with detailed component monitoring
            builder.Services.AddHealthChecks()
                .AddCheck<BiatecTokensApi.HealthChecks.IPFSHealthCheck>("ipfs", tags: new[] { "ipfs", "external" })
                .AddCheck<BiatecTokensApi.HealthChecks.AlgorandNetworkHealthCheck>("algorand", tags: new[] { "algorand", "blockchain", "external" })
                .AddCheck<BiatecTokensApi.HealthChecks.EVMChainHealthCheck>("evm", tags: new[] { "evm", "blockchain", "external" })
                .AddCheck<BiatecTokensApi.HealthChecks.StripeHealthCheck>("stripe", tags: new[] { "stripe", "payment", "external" })
                .AddCheck<BiatecTokensApi.HealthChecks.KeyManagementHealthCheck>("keymanagement", tags: new[] { "keymanagement", "security", "critical" });
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

            builder.Services.Configure<BiatecTokensApi.Configuration.CapabilityMatrixConfig>(
                builder.Configuration.GetSection("CapabilityMatrixConfig"));

            builder.Services.Configure<BiatecTokensApi.Configuration.JwtConfig>(
                builder.Configuration.GetSection("JwtConfig"));

            builder.Services.Configure<BiatecTokensApi.Configuration.KeyManagementConfig>(
                builder.Configuration.GetSection("KeyManagementConfig"));

            builder.Services.Configure<BiatecTokensApi.Configuration.KycConfig>(
                builder.Configuration.GetSection("KycConfig"));

            // Register HTTP client for API calls with resilience policies
            builder.Services.AddHttpClient("default")
                .AddStandardResilienceHandler(options =>
                {
                    // Configure retry policy
                    options.Retry.MaxRetryAttempts = 3;
                    options.Retry.Delay = TimeSpan.FromMilliseconds(500);
                    options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
                    options.Retry.UseJitter = true;
                    
                    // Configure circuit breaker
                    options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);
                    options.CircuitBreaker.FailureRatio = 0.5;
                    options.CircuitBreaker.MinimumThroughput = 10;
                    options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);
                    
                    // Configure timeout
                    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(60);
                    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(20);
                });
            
            // Also register the default HttpClient for backward compatibility
            builder.Services.AddHttpClient();

            // Register repositories
            builder.Services.AddSingleton<IUserRepository, UserRepository>();
            builder.Services.AddSingleton<IComplianceProfileRepository, ComplianceProfileRepository>();
            builder.Services.AddSingleton<IIPFSRepository, IPFSRepository>();
            builder.Services.AddSingleton<IWhitelistRepository, WhitelistRepository>();
            builder.Services.AddSingleton<IWhitelistRulesRepository, WhitelistRulesRepository>();
            builder.Services.AddSingleton<BiatecTokensApi.Repositories.Interface.IComplianceRepository, BiatecTokensApi.Repositories.ComplianceRepository>();
            builder.Services.AddSingleton<BiatecTokensApi.Repositories.Interface.ITokenIssuanceRepository, BiatecTokensApi.Repositories.TokenIssuanceRepository>();
            builder.Services.AddSingleton<BiatecTokensApi.Repositories.Interface.IEnterpriseAuditRepository, BiatecTokensApi.Repositories.EnterpriseAuditRepository>();
            builder.Services.AddSingleton<BiatecTokensApi.Repositories.Interface.IWebhookRepository, BiatecTokensApi.Repositories.WebhookRepository>();
            builder.Services.AddSingleton<BiatecTokensApi.Repositories.Interface.ISubscriptionRepository, BiatecTokensApi.Repositories.SubscriptionRepository>();
            builder.Services.AddSingleton<BiatecTokensApi.Repositories.Interface.IDeploymentStatusRepository, BiatecTokensApi.Repositories.DeploymentStatusRepository>();
            builder.Services.AddSingleton<BiatecTokensApi.Repositories.Interface.IComplianceReportRepository, BiatecTokensApi.Repositories.ComplianceReportRepository>();
            builder.Services.AddSingleton<BiatecTokensApi.Repositories.Interface.ISecurityActivityRepository, BiatecTokensApi.Repositories.SecurityActivityRepository>();
            builder.Services.AddSingleton<BiatecTokensApi.Repositories.Interface.ITokenRegistryRepository, BiatecTokensApi.Repositories.TokenRegistryRepository>();
            builder.Services.AddSingleton<BiatecTokensApi.Repositories.Interface.IJurisdictionRulesRepository, BiatecTokensApi.Repositories.JurisdictionRulesRepository>();
            builder.Services.AddSingleton<BiatecTokensApi.Repositories.Interface.IComplianceDecisionRepository, BiatecTokensApi.Repositories.ComplianceDecisionRepository>();
            builder.Services.AddSingleton<BiatecTokensApi.Repositories.Interface.IKycRepository, BiatecTokensApi.Repositories.KycRepository>();

            // Also register non-interface repositories for ingestion service
            builder.Services.AddSingleton<BiatecTokensApi.Repositories.TokenIssuanceRepository>();
            builder.Services.AddSingleton<BiatecTokensApi.Repositories.ComplianceRepository>();

            // Register metering service
            builder.Services.AddSingleton<ISubscriptionMeteringService, SubscriptionMeteringService>();

            // Register subscription tier service
            builder.Services.AddSingleton<ISubscriptionTierService, SubscriptionTierService>();

            // Register billing service
            builder.Services.AddSingleton<IBillingService, BillingService>();

            // Register the token services
            builder.Services.AddSingleton<IAuthenticationService, AuthenticationService>();
            
            // Register key management providers
            builder.Services.AddSingleton<EnvironmentKeyProvider>();
            builder.Services.AddSingleton<HardcodedKeyProvider>();
            builder.Services.AddSingleton<AzureKeyVaultProvider>();
            builder.Services.AddSingleton<AwsKmsProvider>();
            builder.Services.AddSingleton<KeyProviderFactory>();
            
            builder.Services.AddSingleton<IERC20TokenService, ERC20TokenService>();
            builder.Services.AddSingleton<IARC3TokenService, ARC3TokenService>();
            builder.Services.AddSingleton<IASATokenService, ASATokenService>();
            builder.Services.AddSingleton<IARC200TokenService, ARC200TokenService>();
            builder.Services.AddSingleton<IARC1400TokenService, ARC1400TokenService>();
            builder.Services.AddSingleton<IWhitelistService, WhitelistService>();
            builder.Services.AddSingleton<IWhitelistRulesService, WhitelistRulesService>();
            builder.Services.AddSingleton<IComplianceService, ComplianceService>();
            builder.Services.AddSingleton<IComplianceProfileService, ComplianceProfileService>();
            builder.Services.AddSingleton<IEnterpriseAuditService, EnterpriseAuditService>();
            builder.Services.AddSingleton<IWebhookService, WebhookService>();
            builder.Services.AddSingleton<IStripeService, StripeService>();
            builder.Services.AddSingleton<IDeploymentStatusService, DeploymentStatusService>();
            builder.Services.AddSingleton<IDeploymentAuditService, DeploymentAuditService>();
            builder.Services.AddSingleton<IComplianceReportService, ComplianceReportService>();
            builder.Services.AddSingleton<ISecurityActivityService, SecurityActivityService>();
            builder.Services.AddSingleton<ITokenStandardRegistry, TokenStandardRegistry>();
            builder.Services.AddSingleton<ITokenStandardValidator, TokenStandardValidator>();
            builder.Services.AddSingleton<ITokenRegistryService, TokenRegistryService>();
            builder.Services.AddSingleton<IRegistryIngestionService, RegistryIngestionService>();
            builder.Services.AddSingleton<IJurisdictionRulesService, JurisdictionRulesService>();
            builder.Services.AddSingleton<ICapabilityMatrixService, CapabilityMatrixService>();
            builder.Services.AddSingleton<ITokenMetadataService, TokenMetadataService>();
            builder.Services.AddSingleton<IPolicyEvaluator, PolicyEvaluator>();
            builder.Services.AddSingleton<IComplianceDecisionService, ComplianceDecisionService>();
            builder.Services.AddSingleton<IKycProvider, MockKycProvider>();
            builder.Services.AddSingleton<IKycService, KycService>();

            builder.Services.AddSingleton<IMetricsService, MetricsService>();
            
            // Register decision intelligence service
            builder.Services.AddSingleton<IDecisionIntelligenceService, DecisionIntelligenceService>();

            // Register validation service
            builder.Services.AddSingleton<IValidationService, ValidationService>();

            // Register background workers
            builder.Services.AddHostedService<BiatecTokensApi.Workers.TransactionMonitorWorker>();

            // Configure ARC-0014 Algorand authentication (existing)
            var authOptions = builder.Configuration.GetSection("AlgorandAuthentication").Get<AlgorandAuthenticationOptionsV2>();
            if (authOptions == null) throw new Exception("Config for the authentication is missing");

            // Configure JWT authentication for email/password (new)
            var jwtConfig = builder.Configuration.GetSection("JwtConfig").Get<BiatecTokensApi.Configuration.JwtConfig>();
            if (jwtConfig == null || string.IsNullOrWhiteSpace(jwtConfig.SecretKey))
            {
                // Generate a random secret key for development if not configured
                jwtConfig = new BiatecTokensApi.Configuration.JwtConfig
                {
                    SecretKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(64)),
                    Issuer = "BiatecTokensApi",
                    Audience = "BiatecTokensUsers",
                    AccessTokenExpirationMinutes = 60,
                    RefreshTokenExpirationDays = 30
                };
                builder.Services.Configure<BiatecTokensApi.Configuration.JwtConfig>(options =>
                {
                    options.SecretKey = jwtConfig.SecretKey;
                    options.Issuer = jwtConfig.Issuer;
                    options.Audience = jwtConfig.Audience;
                    options.AccessTokenExpirationMinutes = jwtConfig.AccessTokenExpirationMinutes;
                    options.RefreshTokenExpirationDays = jwtConfig.RefreshTokenExpirationDays;
                });
            }

            builder.Services.AddAuthentication(options =>
            {
                // Set JWT as the default authentication scheme
                options.DefaultAuthenticateScheme = "Bearer";
                options.DefaultChallengeScheme = "Bearer";
            })
            .AddAlgorand(AlgorandAuthenticationHandlerV2.ID, a =>
            {
                a.Realm = authOptions.Realm;
                a.CheckExpiration = authOptions.CheckExpiration;
                a.EmptySuccessOnFailure = authOptions.EmptySuccessOnFailure;
                a.AllowedNetworks = authOptions.AllowedNetworks;
                a.Debug = authOptions.Debug;
            })
            .AddJwtBearer("Bearer", options =>
            {
                options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                {
                    ValidateIssuerSigningKey = jwtConfig.ValidateIssuerSigningKey,
                    IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                        System.Text.Encoding.ASCII.GetBytes(jwtConfig.SecretKey)),
                    ValidateIssuer = jwtConfig.ValidateIssuer,
                    ValidIssuer = jwtConfig.Issuer,
                    ValidateAudience = jwtConfig.ValidateAudience,
                    ValidAudience = jwtConfig.Audience,
                    ValidateLifetime = jwtConfig.ValidateLifetime,
                    ClockSkew = TimeSpan.FromMinutes(jwtConfig.ClockSkewMinutes)
                };
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

            // Add correlation ID middleware (should be first to ensure all requests have IDs)
            app.UseCorrelationId();
            
            // Add global exception handler middleware
            app.UseGlobalExceptionHandler();
            
            // Add metrics middleware to track all requests
            app.UseMetrics();
            
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

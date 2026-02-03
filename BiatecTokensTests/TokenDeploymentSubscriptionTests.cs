using BiatecTokensApi.Filters;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Subscription;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System.Security.Claims;

namespace BiatecTokensTests
{
    /// <summary>
    /// Unit tests for token deployment subscription enforcement
    /// </summary>
    [TestFixture]
    public class TokenDeploymentSubscriptionTests
    {
        private Mock<ISubscriptionTierService> _mockTierService = null!;
        private Mock<ILogger<TokenDeploymentSubscriptionAttribute>> _mockLogger = null!;

        [SetUp]
        public void Setup()
        {
            _mockTierService = new Mock<ISubscriptionTierService>();
            _mockLogger = new Mock<ILogger<TokenDeploymentSubscriptionAttribute>>();
        }

        #region Free Tier Tests

        [Test]
        public async Task TokenDeployment_FreeTier_WithinLimit_AllowsDeployment()
        {
            // Arrange
            var attribute = new TokenDeploymentSubscriptionAttribute();
            var context = CreateActionExecutingContext("test-user@example.com");
            var next = new ActionExecutionDelegate(() => Task.FromResult(CreateSuccessfulActionExecutedContext(context)));

            _mockTierService.Setup(x => x.CanDeployTokenAsync(It.IsAny<string>()))
                .ReturnsAsync(true);

            _mockTierService.Setup(x => x.GetUserTierAsync(It.IsAny<string>()))
                .ReturnsAsync(SubscriptionTier.Free);

            _mockTierService.Setup(x => x.RecordTokenDeploymentAsync(It.IsAny<string>()))
                .ReturnsAsync(true);

            _mockTierService.Setup(x => x.GetTokenDeploymentCountAsync(It.IsAny<string>()))
                .ReturnsAsync(2); // After deployment: 2 deployments

            // Act
            await attribute.OnActionExecutionAsync(context, next);

            // Assert
            Assert.That(context.Result, Is.Null, "Free tier should allow deployment within limit");
            
            // Verify deployment was recorded
            _mockTierService.Verify(x => x.RecordTokenDeploymentAsync("test-user@example.com"), Times.Once);
        }

        [Test]
        public async Task TokenDeployment_FreeTier_ExceedsLimit_BlocksDeployment()
        {
            // Arrange
            var attribute = new TokenDeploymentSubscriptionAttribute();
            var context = CreateActionExecutingContext("test-user@example.com");
            var next = new ActionExecutionDelegate(() => Task.FromResult(CreateSuccessfulActionExecutedContext(context)));

            _mockTierService.Setup(x => x.CanDeployTokenAsync(It.IsAny<string>()))
                .ReturnsAsync(false);

            _mockTierService.Setup(x => x.GetUserTierAsync(It.IsAny<string>()))
                .ReturnsAsync(SubscriptionTier.Free);

            _mockTierService.Setup(x => x.GetTierLimits(SubscriptionTier.Free))
                .Returns(new SubscriptionTierLimits
                {
                    Tier = SubscriptionTier.Free,
                    TierName = "Free",
                    MaxTokenDeployments = 3,
                    Description = "Free tier with up to 3 token deployments"
                });

            _mockTierService.Setup(x => x.GetTokenDeploymentCountAsync(It.IsAny<string>()))
                .ReturnsAsync(3); // Already at limit

            // Act
            await attribute.OnActionExecutionAsync(context, next);

            // Assert
            Assert.That(context.Result, Is.Not.Null, "Free tier should block deployment when limit reached");
            Assert.That(context.Result, Is.InstanceOf<ObjectResult>());

            var objectResult = context.Result as ObjectResult;
            Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status402PaymentRequired));
            Assert.That(objectResult.Value, Is.InstanceOf<ApiErrorResponse>());

            var errorResponse = objectResult.Value as ApiErrorResponse;
            Assert.That(errorResponse!.ErrorCode, Is.EqualTo(ErrorCodes.SUBSCRIPTION_LIMIT_REACHED));
            Assert.That(errorResponse.ErrorMessage, Does.Contain("Free"));
            Assert.That(errorResponse.ErrorMessage, Does.Contain("3"));
            Assert.That(errorResponse.RemediationHint, Does.Contain("upgrade"));

            // Verify deployment was NOT recorded
            _mockTierService.Verify(x => x.RecordTokenDeploymentAsync(It.IsAny<string>()), Times.Never);
        }

        #endregion

        #region Basic Tier Tests

        [Test]
        public async Task TokenDeployment_BasicTier_WithinLimit_AllowsDeployment()
        {
            // Arrange
            var attribute = new TokenDeploymentSubscriptionAttribute();
            var context = CreateActionExecutingContext("basic-user@example.com");
            var next = new ActionExecutionDelegate(() => Task.FromResult(CreateSuccessfulActionExecutedContext(context)));

            _mockTierService.Setup(x => x.CanDeployTokenAsync(It.IsAny<string>()))
                .ReturnsAsync(true);

            _mockTierService.Setup(x => x.RecordTokenDeploymentAsync(It.IsAny<string>()))
                .ReturnsAsync(true);

            _mockTierService.Setup(x => x.GetTokenDeploymentCountAsync(It.IsAny<string>()))
                .ReturnsAsync(8); // After deployment: 8 of 10

            // Act
            await attribute.OnActionExecutionAsync(context, next);

            // Assert
            Assert.That(context.Result, Is.Null, "Basic tier should allow deployment within limit");
            _mockTierService.Verify(x => x.RecordTokenDeploymentAsync("basic-user@example.com"), Times.Once);
        }

        #endregion

        #region Premium Tier Tests

        [Test]
        public async Task TokenDeployment_PremiumTier_WithinLimit_AllowsDeployment()
        {
            // Arrange
            var attribute = new TokenDeploymentSubscriptionAttribute();
            var context = CreateActionExecutingContext("premium-user@example.com");
            var next = new ActionExecutionDelegate(() => Task.FromResult(CreateSuccessfulActionExecutedContext(context)));

            _mockTierService.Setup(x => x.CanDeployTokenAsync(It.IsAny<string>()))
                .ReturnsAsync(true);

            _mockTierService.Setup(x => x.RecordTokenDeploymentAsync(It.IsAny<string>()))
                .ReturnsAsync(true);

            _mockTierService.Setup(x => x.GetTokenDeploymentCountAsync(It.IsAny<string>()))
                .ReturnsAsync(40); // After deployment: 40 of 50

            // Act
            await attribute.OnActionExecutionAsync(context, next);

            // Assert
            Assert.That(context.Result, Is.Null, "Premium tier should allow deployment within limit");
            _mockTierService.Verify(x => x.RecordTokenDeploymentAsync("premium-user@example.com"), Times.Once);
        }

        #endregion

        #region Enterprise Tier Tests

        [Test]
        public async Task TokenDeployment_EnterpriseTier_UnlimitedDeployments()
        {
            // Arrange
            var attribute = new TokenDeploymentSubscriptionAttribute();
            var context = CreateActionExecutingContext("enterprise-user@example.com");
            var next = new ActionExecutionDelegate(() => Task.FromResult(CreateSuccessfulActionExecutedContext(context)));

            _mockTierService.Setup(x => x.CanDeployTokenAsync(It.IsAny<string>()))
                .ReturnsAsync(true);

            _mockTierService.Setup(x => x.GetUserTierAsync(It.IsAny<string>()))
                .ReturnsAsync(SubscriptionTier.Enterprise);

            _mockTierService.Setup(x => x.RecordTokenDeploymentAsync(It.IsAny<string>()))
                .ReturnsAsync(true);

            _mockTierService.Setup(x => x.GetTokenDeploymentCountAsync(It.IsAny<string>()))
                .ReturnsAsync(100); // Even with high count, should allow

            // Act
            await attribute.OnActionExecutionAsync(context, next);

            // Assert
            Assert.That(context.Result, Is.Null, "Enterprise tier should allow unlimited deployments");
            _mockTierService.Verify(x => x.RecordTokenDeploymentAsync("enterprise-user@example.com"), Times.Once);
        }

        #endregion

        #region Authentication Tests

        [Test]
        public async Task TokenDeployment_NoAuthentication_ReturnsUnauthorized()
        {
            // Arrange
            var attribute = new TokenDeploymentSubscriptionAttribute();
            var context = CreateActionExecutingContext(null); // No user identity
            var next = new ActionExecutionDelegate(() => Task.FromResult(CreateSuccessfulActionExecutedContext(context)));

            // Act
            await attribute.OnActionExecutionAsync(context, next);

            // Assert
            Assert.That(context.Result, Is.Not.Null);
            Assert.That(context.Result, Is.InstanceOf<UnauthorizedObjectResult>());

            var result = context.Result as UnauthorizedObjectResult;
            Assert.That(result!.Value, Is.InstanceOf<ApiErrorResponse>());

            var errorResponse = result.Value as ApiErrorResponse;
            Assert.That(errorResponse!.ErrorCode, Is.EqualTo(ErrorCodes.UNAUTHORIZED));
            Assert.That(errorResponse.RemediationHint, Does.Contain("ARC-0014"));
        }

        #endregion

        #region Error Response Tests

        [Test]
        public async Task TokenDeployment_LimitReached_IncludesCorrelationId()
        {
            // Arrange
            var attribute = new TokenDeploymentSubscriptionAttribute();
            var correlationId = "test-correlation-123";
            var context = CreateActionExecutingContext("test-user@example.com", correlationId);
            var next = new ActionExecutionDelegate(() => Task.FromResult(CreateSuccessfulActionExecutedContext(context)));

            _mockTierService.Setup(x => x.CanDeployTokenAsync(It.IsAny<string>()))
                .ReturnsAsync(false);

            _mockTierService.Setup(x => x.GetUserTierAsync(It.IsAny<string>()))
                .ReturnsAsync(SubscriptionTier.Free);

            _mockTierService.Setup(x => x.GetTierLimits(SubscriptionTier.Free))
                .Returns(new SubscriptionTierLimits
                {
                    Tier = SubscriptionTier.Free,
                    TierName = "Free",
                    MaxTokenDeployments = 3
                });

            _mockTierService.Setup(x => x.GetTokenDeploymentCountAsync(It.IsAny<string>()))
                .ReturnsAsync(3);

            // Act
            await attribute.OnActionExecutionAsync(context, next);

            // Assert
            var objectResult = context.Result as ObjectResult;
            var errorResponse = objectResult!.Value as ApiErrorResponse;
            Assert.That(errorResponse!.CorrelationId, Is.EqualTo(correlationId),
                "Error response should include correlation ID");
        }

        [Test]
        public async Task TokenDeployment_LimitReached_IncludesDetailedInformation()
        {
            // Arrange
            var attribute = new TokenDeploymentSubscriptionAttribute();
            var context = CreateActionExecutingContext("test-user@example.com");
            var next = new ActionExecutionDelegate(() => Task.FromResult(CreateSuccessfulActionExecutedContext(context)));

            _mockTierService.Setup(x => x.CanDeployTokenAsync(It.IsAny<string>()))
                .ReturnsAsync(false);

            _mockTierService.Setup(x => x.GetUserTierAsync(It.IsAny<string>()))
                .ReturnsAsync(SubscriptionTier.Basic);

            _mockTierService.Setup(x => x.GetTierLimits(SubscriptionTier.Basic))
                .Returns(new SubscriptionTierLimits
                {
                    Tier = SubscriptionTier.Basic,
                    TierName = "Basic",
                    MaxTokenDeployments = 10,
                    Description = "Basic tier with 10 deployments"
                });

            _mockTierService.Setup(x => x.GetTokenDeploymentCountAsync(It.IsAny<string>()))
                .ReturnsAsync(10);

            // Act
            await attribute.OnActionExecutionAsync(context, next);

            // Assert
            var objectResult = context.Result as ObjectResult;
            var errorResponse = objectResult!.Value as ApiErrorResponse;
            
            Assert.That(errorResponse!.Details, Is.Not.Null);
            Assert.That(errorResponse.Details, Contains.Key("currentTier"));
            Assert.That(errorResponse.Details, Contains.Key("currentDeployments"));
            Assert.That(errorResponse.Details, Contains.Key("maxDeployments"));
            Assert.That(errorResponse.Details, Contains.Key("tierDescription"));

            Assert.That(errorResponse.Details!["currentDeployments"], Is.EqualTo(10));
            Assert.That(errorResponse.Details["maxDeployments"], Is.EqualTo(10));
        }

        #endregion

        #region Recording Tests

        [Test]
        public async Task TokenDeployment_FailedRequest_DoesNotRecordDeployment()
        {
            // Arrange
            var attribute = new TokenDeploymentSubscriptionAttribute();
            var context = CreateActionExecutingContext("test-user@example.com");
            var next = new ActionExecutionDelegate(() => Task.FromResult(CreateFailedActionExecutedContext(context)));

            _mockTierService.Setup(x => x.CanDeployTokenAsync(It.IsAny<string>()))
                .ReturnsAsync(true);

            // Act
            await attribute.OnActionExecutionAsync(context, next);

            // Assert
            _mockTierService.Verify(x => x.RecordTokenDeploymentAsync(It.IsAny<string>()), Times.Never,
                "Failed deployment should not be recorded");
        }

        [Test]
        public async Task TokenDeployment_SuccessfulRequest_RecordsDeployment()
        {
            // Arrange
            var attribute = new TokenDeploymentSubscriptionAttribute();
            var context = CreateActionExecutingContext("test-user@example.com");
            var next = new ActionExecutionDelegate(() => Task.FromResult(CreateSuccessfulActionExecutedContext(context)));

            _mockTierService.Setup(x => x.CanDeployTokenAsync(It.IsAny<string>()))
                .ReturnsAsync(true);

            _mockTierService.Setup(x => x.RecordTokenDeploymentAsync(It.IsAny<string>()))
                .ReturnsAsync(true);

            _mockTierService.Setup(x => x.GetTokenDeploymentCountAsync(It.IsAny<string>()))
                .ReturnsAsync(1);

            // Act
            await attribute.OnActionExecutionAsync(context, next);

            // Assert
            _mockTierService.Verify(x => x.RecordTokenDeploymentAsync("test-user@example.com"), Times.Once,
                "Successful deployment should be recorded");
        }

        #endregion

        #region Helper Methods

        private ActionExecutingContext CreateActionExecutingContext(string? userName, string? correlationId = null)
        {
            var httpContext = new DefaultHttpContext();
            
            // Set up user identity if provided
            if (!string.IsNullOrEmpty(userName))
            {
                var claims = new List<Claim> { new Claim(ClaimTypes.Name, userName) };
                var identity = new ClaimsIdentity(claims, "TestAuth");
                httpContext.User = new ClaimsPrincipal(identity);
            }

            // Set correlation ID
            httpContext.TraceIdentifier = correlationId ?? Guid.NewGuid().ToString();

            // Set up services
            var services = new ServiceCollection();
            services.AddSingleton(_mockTierService.Object);
            services.AddSingleton(_mockLogger.Object);
            httpContext.RequestServices = services.BuildServiceProvider();

            var actionContext = new ActionContext(
                httpContext,
                new RouteData(),
                new ActionDescriptor()
            );

            return new ActionExecutingContext(
                actionContext,
                new List<IFilterMetadata>(),
                new Dictionary<string, object?>(),
                controller: null!
            );
        }

        private ActionExecutedContext CreateSuccessfulActionExecutedContext(ActionExecutingContext executingContext)
        {
            // Set the response status code on the HttpContext
            executingContext.HttpContext.Response.StatusCode = StatusCodes.Status200OK;
            
            return new ActionExecutedContext(
                executingContext,
                new List<IFilterMetadata>(),
                controller: null!)
            {
                Result = new ObjectResult(new { success = true })
                {
                    StatusCode = StatusCodes.Status200OK
                }
            };
        }

        private ActionExecutedContext CreateFailedActionExecutedContext(ActionExecutingContext executingContext)
        {
            // Set the response status code on the HttpContext
            executingContext.HttpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
            
            return new ActionExecutedContext(
                executingContext,
                new List<IFilterMetadata>(),
                controller: null!)
            {
                Result = new ObjectResult(new { success = false, error = "Deployment failed" })
                {
                    StatusCode = StatusCodes.Status500InternalServerError
                }
            };
        }

        #endregion
    }
}

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
    /// Tests for backend stability features including error response consistency,
    /// correlation IDs, and subscription tier validation.
    /// </summary>
    [TestFixture]
    public class BackendStabilityTests
    {
        private Mock<ISubscriptionTierService> _mockTierService = null!;
        private Mock<ILogger<SubscriptionTierValidationAttribute>> _mockLogger = null!;

        [SetUp]
        public void Setup()
        {
            _mockTierService = new Mock<ISubscriptionTierService>();
            _mockLogger = new Mock<ILogger<SubscriptionTierValidationAttribute>>();
        }

        #region BaseResponse CorrelationId Tests

        [Test]
        public void BaseResponse_IncludesCorrelationId()
        {
            // Arrange
            var correlationId = Guid.NewGuid().ToString();

            // Act
            var response = new BaseResponse
            {
                Success = true,
                CorrelationId = correlationId
            };

            // Assert
            Assert.That(response.CorrelationId, Is.EqualTo(correlationId),
                "BaseResponse should include correlation ID");
            Assert.That(response.Success, Is.True);
        }

        [Test]
        public void BaseResponse_WithError_IncludesCorrelationId()
        {
            // Arrange
            var correlationId = Guid.NewGuid().ToString();

            // Act
            var response = new BaseResponse
            {
                Success = false,
                ErrorCode = ErrorCodes.TRANSACTION_FAILED,
                ErrorMessage = "Transaction failed",
                CorrelationId = correlationId
            };

            // Assert
            Assert.That(response.CorrelationId, Is.EqualTo(correlationId),
                "Error response should include correlation ID");
            Assert.That(response.Success, Is.False);
            Assert.That(response.ErrorCode, Is.EqualTo(ErrorCodes.TRANSACTION_FAILED));
        }

        #endregion

        #region ApiErrorResponse Tests

        [Test]
        public void ApiErrorResponse_ContainsAllRequiredFields()
        {
            // Arrange
            var correlationId = "test-correlation-123";
            var path = "/api/v1/token/create";

            // Act
            var response = new ApiErrorResponse
            {
                Success = false,
                ErrorCode = ErrorCodes.INVALID_REQUEST,
                ErrorMessage = "Invalid request parameters",
                RemediationHint = "Check your request parameters",
                CorrelationId = correlationId,
                Path = path,
                Timestamp = DateTime.UtcNow
            };

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.ErrorCode, Is.EqualTo(ErrorCodes.INVALID_REQUEST));
            Assert.That(response.ErrorMessage, Is.EqualTo("Invalid request parameters"));
            Assert.That(response.RemediationHint, Is.Not.Null);
            Assert.That(response.CorrelationId, Is.EqualTo(correlationId));
            Assert.That(response.Path, Is.EqualTo(path));
            Assert.That(response.Timestamp, Is.Not.EqualTo(default(DateTime)));
        }

        #endregion

        #region Subscription Tier Validation Tests

        [Test]
        public async Task SubscriptionTierValidation_FreeTier_AllowsBasicOperations()
        {
            // Arrange
            var attribute = new SubscriptionTierValidationAttribute
            {
                MinimumTier = "Free",
                RequiresPremium = false
            };

            var context = CreateActionExecutingContext("test-user@example.com");
            var next = new ActionExecutionDelegate(() => Task.FromResult(CreateActionExecutedContext(context)));

            _mockTierService.Setup(x => x.GetUserTierAsync(It.IsAny<string>()))
                .ReturnsAsync(SubscriptionTier.Free);

            _mockTierService.Setup(x => x.GetTierLimits(SubscriptionTier.Free))
                .Returns(new SubscriptionTierLimits
                {
                    TierName = "Free",
                    MaxAddressesPerAsset = 10,
                    BulkOperationsEnabled = false,
                    AuditLogEnabled = false
                });

            // Act
            await attribute.OnActionExecutionAsync(context, next);

            // Assert
            Assert.That(context.Result, Is.Null, "Free tier should allow basic operations");
        }

        [Test]
        public async Task SubscriptionTierValidation_FreeTier_BlocksPremiumFeatures()
        {
            // Arrange
            var attribute = new SubscriptionTierValidationAttribute
            {
                RequiresPremium = true
            };

            var context = CreateActionExecutingContext("test-user@example.com");
            var next = new ActionExecutionDelegate(() => Task.FromResult(CreateActionExecutedContext(context)));

            _mockTierService.Setup(x => x.GetUserTierAsync(It.IsAny<string>()))
                .ReturnsAsync(SubscriptionTier.Free);

            _mockTierService.Setup(x => x.GetTierLimits(SubscriptionTier.Free))
                .Returns(new SubscriptionTierLimits
                {
                    TierName = "Free",
                    MaxAddressesPerAsset = 10,
                    BulkOperationsEnabled = false,
                    AuditLogEnabled = false
                });

            // Act
            await attribute.OnActionExecutionAsync(context, next);

            // Assert
            Assert.That(context.Result, Is.Not.Null, "Free tier should be blocked from premium features");
            Assert.That(context.Result, Is.InstanceOf<ObjectResult>());

            var objectResult = context.Result as ObjectResult;
            Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status402PaymentRequired));
            Assert.That(objectResult.Value, Is.InstanceOf<ApiErrorResponse>());

            var errorResponse = objectResult.Value as ApiErrorResponse;
            Assert.That(errorResponse!.ErrorCode, Is.EqualTo(ErrorCodes.SUBSCRIPTION_LIMIT_REACHED));
            Assert.That(errorResponse.RemediationHint, Does.Contain("upgrade"));
        }

        [Test]
        public async Task SubscriptionTierValidation_PremiumTier_AllowsPremiumFeatures()
        {
            // Arrange
            var attribute = new SubscriptionTierValidationAttribute
            {
                RequiresPremium = true
            };

            var context = CreateActionExecutingContext("premium-user@example.com");
            var next = new ActionExecutionDelegate(() => Task.FromResult(CreateActionExecutedContext(context)));

            _mockTierService.Setup(x => x.GetUserTierAsync(It.IsAny<string>()))
                .ReturnsAsync(SubscriptionTier.Premium);

            _mockTierService.Setup(x => x.GetTierLimits(SubscriptionTier.Premium))
                .Returns(new SubscriptionTierLimits
                {
                    TierName = "Premium",
                    MaxAddressesPerAsset = 1000,
                    BulkOperationsEnabled = true,
                    AuditLogEnabled = true
                });

            // Act
            await attribute.OnActionExecutionAsync(context, next);

            // Assert
            Assert.That(context.Result, Is.Null, "Premium tier should allow premium features");
        }

        [Test]
        public async Task SubscriptionTierValidation_NoAuthentication_ReturnsUnauthorized()
        {
            // Arrange
            var attribute = new SubscriptionTierValidationAttribute();
            var context = CreateActionExecutingContext(null); // No user identity
            var next = new ActionExecutionDelegate(() => Task.FromResult(CreateActionExecutedContext(context)));

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

        [Test]
        public async Task SubscriptionTierValidation_IncludesCorrelationId()
        {
            // Arrange
            var attribute = new SubscriptionTierValidationAttribute
            {
                RequiresPremium = true
            };

            var correlationId = "test-correlation-456";
            var context = CreateActionExecutingContext("test-user@example.com", correlationId);
            var next = new ActionExecutionDelegate(() => Task.FromResult(CreateActionExecutedContext(context)));

            _mockTierService.Setup(x => x.GetUserTierAsync(It.IsAny<string>()))
                .ReturnsAsync(SubscriptionTier.Free);

            _mockTierService.Setup(x => x.GetTierLimits(SubscriptionTier.Free))
                .Returns(new SubscriptionTierLimits
                {
                    TierName = "Free",
                    BulkOperationsEnabled = false
                });

            // Act
            await attribute.OnActionExecutionAsync(context, next);

            // Assert
            var objectResult = context.Result as ObjectResult;
            var errorResponse = objectResult!.Value as ApiErrorResponse;
            Assert.That(errorResponse!.CorrelationId, Is.EqualTo(correlationId),
                "Error response should include correlation ID");
        }

        #endregion

        #region Error Code Tests

        [Test]
        public void ErrorCodes_ContainsCriticalCodes()
        {
            // Assert - Verify all critical error codes are defined
            Assert.That(ErrorCodes.INVALID_REQUEST, Is.Not.Null.And.Not.Empty);
            Assert.That(ErrorCodes.UNAUTHORIZED, Is.Not.Null.And.Not.Empty);
            Assert.That(ErrorCodes.FORBIDDEN, Is.Not.Null.And.Not.Empty);
            Assert.That(ErrorCodes.NOT_FOUND, Is.Not.Null.And.Not.Empty);
            Assert.That(ErrorCodes.TRANSACTION_FAILED, Is.Not.Null.And.Not.Empty);
            Assert.That(ErrorCodes.BLOCKCHAIN_CONNECTION_ERROR, Is.Not.Null.And.Not.Empty);
            Assert.That(ErrorCodes.IPFS_SERVICE_ERROR, Is.Not.Null.And.Not.Empty);
            Assert.That(ErrorCodes.TIMEOUT, Is.Not.Null.And.Not.Empty);
            Assert.That(ErrorCodes.INTERNAL_SERVER_ERROR, Is.Not.Null.And.Not.Empty);
            Assert.That(ErrorCodes.SUBSCRIPTION_LIMIT_REACHED, Is.Not.Null.And.Not.Empty);
            Assert.That(ErrorCodes.RATE_LIMIT_EXCEEDED, Is.Not.Null.And.Not.Empty);
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

        private ActionExecutedContext CreateActionExecutedContext(ActionExecutingContext executingContext)
        {
            return new ActionExecutedContext(
                executingContext,
                new List<IFilterMetadata>(),
                controller: null!
            );
        }

        #endregion
    }
}

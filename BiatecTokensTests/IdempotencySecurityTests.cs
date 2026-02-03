using BiatecTokensApi.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Unit tests for idempotency key security and functionality
    /// </summary>
    [TestFixture]
    public class IdempotencySecurityTests
    {
        private Mock<ILogger<IdempotencyKeyAttribute>> _mockLogger = null!;

        [SetUp]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger<IdempotencyKeyAttribute>>();
        }

        #region Basic Idempotency Tests

        [Test]
        public async Task Idempotency_NoKey_ProceedsNormally()
        {
            // Arrange
            var attribute = new IdempotencyKeyAttribute();
            var context = CreateActionExecutingContext(null, new { name = "Test Token" });
            var next = new ActionExecutionDelegate(() => Task.FromResult(CreateSuccessfulActionExecutedContext(context)));

            // Act
            await attribute.OnActionExecutionAsync(context, next);

            // Assert
            Assert.That(context.Result, Is.Null, "Request without idempotency key should proceed normally");
        }

        [Test]
        public async Task Idempotency_SameKeyAndParameters_ReturnsCachedResponse()
        {
            // Arrange
            var attribute = new IdempotencyKeyAttribute();
            var idempotencyKey = "test-key-123";
            var requestParams = new { name = "Test Token", symbol = "TST" };

            // First request
            var context1 = CreateActionExecutingContext(idempotencyKey, requestParams);
            var next1 = new ActionExecutionDelegate(() => Task.FromResult(CreateSuccessfulActionExecutedContext(context1, new { assetId = 12345 })));
            await attribute.OnActionExecutionAsync(context1, next1);

            // Second request with same key and parameters
            var context2 = CreateActionExecutingContext(idempotencyKey, requestParams);
            var next2 = new ActionExecutionDelegate(() => Task.FromResult(CreateSuccessfulActionExecutedContext(context2, new { assetId = 67890 })));

            // Act
            await attribute.OnActionExecutionAsync(context2, next2);

            // Assert
            Assert.That(context2.Result, Is.Not.Null, "Second request should return cached response");
            Assert.That(context2.Result, Is.InstanceOf<ObjectResult>());

            var objectResult = context2.Result as ObjectResult;
            var response = objectResult!.Value as dynamic;
            
            // Should return the first response (12345), not the second (67890)
            Assert.That((int)response!.assetId, Is.EqualTo(12345), "Should return cached response from first request");
            
            // Check idempotency hit header
            Assert.That(context2.HttpContext.Response.Headers["X-Idempotency-Hit"].ToString(), Is.EqualTo("true"));
        }

        #endregion

        #region Security Tests - Parameter Validation

        [Test]
        public async Task Idempotency_SameKeyDifferentParameters_RejectsRequest()
        {
            // Arrange
            var attribute = new IdempotencyKeyAttribute();
            var idempotencyKey = "test-key-456";

            // First request
            var context1 = CreateActionExecutingContext(idempotencyKey, new { name = "Token A", symbol = "TKA" });
            var next1 = new ActionExecutionDelegate(() => Task.FromResult(CreateSuccessfulActionExecutedContext(context1)));
            await attribute.OnActionExecutionAsync(context1, next1);

            // Second request with same key but different parameters
            var context2 = CreateActionExecutingContext(idempotencyKey, new { name = "Token B", symbol = "TKB" });
            var next2 = new ActionExecutionDelegate(() => Task.FromResult(CreateSuccessfulActionExecutedContext(context2)));

            // Act
            await attribute.OnActionExecutionAsync(context2, next2);

            // Assert
            Assert.That(context2.Result, Is.Not.Null, "Request with mismatched parameters should be rejected");
            Assert.That(context2.Result, Is.InstanceOf<BadRequestObjectResult>());

            var badRequestResult = context2.Result as BadRequestObjectResult;
            var json = System.Text.Json.JsonSerializer.Serialize(badRequestResult!.Value);
            var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.That(root.GetProperty("success").GetBoolean(), Is.False);
            Assert.That(root.GetProperty("errorCode").GetString(), Is.EqualTo("IDEMPOTENCY_KEY_MISMATCH"));
            Assert.That(root.GetProperty("errorMessage").GetString(), Does.Contain("different request parameters"));
        }

        [Test]
        public async Task Idempotency_DifferentParameterOrder_TreatedAsDifferent()
        {
            // Arrange
            var attribute = new IdempotencyKeyAttribute();
            var idempotencyKey = "test-key-789";

            // First request
            var context1 = CreateActionExecutingContext(idempotencyKey, new { name = "Test", amount = 100 });
            var next1 = new ActionExecutionDelegate(() => Task.FromResult(CreateSuccessfulActionExecutedContext(context1)));
            await attribute.OnActionExecutionAsync(context1, next1);

            // Second request with parameters in different order but same values
            // Note: In C#, anonymous objects with different property order create different types
            // So we need to test with a dictionary instead
            var context2 = CreateActionExecutingContext(idempotencyKey, new { amount = 100, name = "Test" });
            var next2 = new ActionExecutionDelegate(() => Task.FromResult(CreateSuccessfulActionExecutedContext(context2)));

            // Act
            await attribute.OnActionExecutionAsync(context2, next2);

            // Assert
            // Different parameter order should be treated as different request
            Assert.That(context2.Result, Is.InstanceOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task Idempotency_SameKeyNullVsEmptyString_TreatedAsDifferent()
        {
            // Arrange
            var attribute = new IdempotencyKeyAttribute();
            var idempotencyKey = "test-key-null";

            // First request with null
            var context1 = CreateActionExecutingContext(idempotencyKey, new { name = (string?)null });
            var next1 = new ActionExecutionDelegate(() => Task.FromResult(CreateSuccessfulActionExecutedContext(context1)));
            await attribute.OnActionExecutionAsync(context1, next1);

            // Second request with empty string
            var context2 = CreateActionExecutingContext(idempotencyKey, new { name = "" });
            var next2 = new ActionExecutionDelegate(() => Task.FromResult(CreateSuccessfulActionExecutedContext(context2)));

            // Act
            await attribute.OnActionExecutionAsync(context2, next2);

            // Assert
            Assert.That(context2.Result, Is.InstanceOf<BadRequestObjectResult>(),
                "null and empty string should be treated as different values");
        }

        #endregion

        #region Expiration Tests

        [Test]
        public async Task Idempotency_ExpiredEntry_AllowsNewRequest()
        {
            // Arrange
            var attribute = new IdempotencyKeyAttribute
            {
                Expiration = TimeSpan.FromMilliseconds(100) // Very short expiration for testing
            };
            var idempotencyKey = "test-key-expire";
            var requestParams = new { name = "Test" };

            // First request
            var context1 = CreateActionExecutingContext(idempotencyKey, requestParams);
            var next1 = new ActionExecutionDelegate(() => Task.FromResult(CreateSuccessfulActionExecutedContext(context1, new { id = 1 })));
            await attribute.OnActionExecutionAsync(context1, next1);

            // Verify first request result
            var result1 = context1.HttpContext.Response.Headers["X-Idempotency-Hit"].ToString();
            Assert.That(result1, Is.EqualTo("false"), "First request should be a cache miss");

            // Wait for expiration
            await Task.Delay(150);

            // Second request after expiration
            var context2 = CreateActionExecutingContext(idempotencyKey, requestParams);
            var next2 = new ActionExecutionDelegate(() => Task.FromResult(CreateSuccessfulActionExecutedContext(context2, new { id = 2 })));

            // Act
            await attribute.OnActionExecutionAsync(context2, next2);

            // Assert - After expiration, should execute as new request (cache miss)
            var result2 = context2.HttpContext.Response.Headers["X-Idempotency-Hit"].ToString();
            Assert.That(result2, Is.EqualTo("false"), "Expired entry should allow new request (cache miss)");
        }

        #endregion

        #region Header Tests

        [Test]
        public async Task Idempotency_CacheHit_SetsHeaderToTrue()
        {
            // Arrange
            var attribute = new IdempotencyKeyAttribute();
            var idempotencyKey = "test-header-hit";
            var requestParams = new { name = "Test" };

            // First request
            var context1 = CreateActionExecutingContext(idempotencyKey, requestParams);
            var next1 = new ActionExecutionDelegate(() => Task.FromResult(CreateSuccessfulActionExecutedContext(context1)));
            await attribute.OnActionExecutionAsync(context1, next1);

            // Second request (cache hit)
            var context2 = CreateActionExecutingContext(idempotencyKey, requestParams);
            var next2 = new ActionExecutionDelegate(() => Task.FromResult(CreateSuccessfulActionExecutedContext(context2)));

            // Act
            await attribute.OnActionExecutionAsync(context2, next2);

            // Assert
            Assert.That(context2.HttpContext.Response.Headers["X-Idempotency-Hit"].ToString(), Is.EqualTo("true"));
        }

        [Test]
        public async Task Idempotency_CacheMiss_SetsHeaderToFalse()
        {
            // Arrange
            var attribute = new IdempotencyKeyAttribute();
            var idempotencyKey = "test-header-miss";
            var requestParams = new { name = "Test" };

            var context = CreateActionExecutingContext(idempotencyKey, requestParams);
            var next = new ActionExecutionDelegate(() => Task.FromResult(CreateSuccessfulActionExecutedContext(context)));

            // Act
            await attribute.OnActionExecutionAsync(context, next);

            // Assert
            Assert.That(context.HttpContext.Response.Headers["X-Idempotency-Hit"].ToString(), Is.EqualTo("false"));
        }

        #endregion

        #region Helper Methods

        private ActionExecutingContext CreateActionExecutingContext(string? idempotencyKey, object? arguments)
        {
            var httpContext = new DefaultHttpContext();
            
            // Set idempotency key header if provided
            if (!string.IsNullOrEmpty(idempotencyKey))
            {
                httpContext.Request.Headers["Idempotency-Key"] = idempotencyKey;
            }

            // Set correlation ID
            httpContext.TraceIdentifier = Guid.NewGuid().ToString();

            // Set up services
            var services = new ServiceCollection();
            services.AddSingleton(_mockLogger.Object);
            httpContext.RequestServices = services.BuildServiceProvider();

            var actionContext = new ActionContext(
                httpContext,
                new RouteData(),
                new ActionDescriptor()
            );

            // Create action arguments dictionary
            var actionArguments = new Dictionary<string, object?>();
            if (arguments != null)
            {
                actionArguments["request"] = arguments;
            }

            return new ActionExecutingContext(
                actionContext,
                new List<IFilterMetadata>(),
                actionArguments,
                controller: null!
            );
        }

        private ActionExecutedContext CreateSuccessfulActionExecutedContext(ActionExecutingContext executingContext, object? response = null)
        {
            return new ActionExecutedContext(
                executingContext,
                new List<IFilterMetadata>(),
                controller: null!)
            {
                Result = new ObjectResult(response ?? new { success = true })
                {
                    StatusCode = StatusCodes.Status200OK
                }
            };
        }

        #endregion
    }
}

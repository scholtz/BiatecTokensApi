using BiatecTokensApi.Middleware;
using BiatecTokensApi.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System.Text.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Unit tests for GlobalExceptionHandlerMiddleware
    /// Tests error handling, logging sanitization, and response formatting
    /// </summary>
    [TestFixture]
    public class GlobalExceptionHandlerMiddlewareTests
    {
        private Mock<ILogger<GlobalExceptionHandlerMiddleware>> _mockLogger = null!;
        private Mock<IHostEnvironment> _mockEnv = null!;
        private DefaultHttpContext _httpContext = null!;

        [SetUp]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger<GlobalExceptionHandlerMiddleware>>();
            _mockEnv = new Mock<IHostEnvironment>();
            _httpContext = new DefaultHttpContext();
            _httpContext.Response.Body = new MemoryStream();
        }

        [Test]
        public async Task InvokeAsync_NoException_CallsNextMiddleware()
        {
            // Arrange
            var nextCalled = false;
            RequestDelegate next = (HttpContext hc) =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            };

            var middleware = new GlobalExceptionHandlerMiddleware(next, _mockLogger.Object, _mockEnv.Object);

            // Act
            await middleware.InvokeAsync(_httpContext);

            // Assert
            Assert.That(nextCalled, Is.True, "Next middleware should be called");
        }

        [Test]
        public async Task InvokeAsync_WithException_ReturnsErrorResponse()
        {
            // Arrange
            var exceptionMessage = "Test exception";
            RequestDelegate next = (HttpContext hc) => throw new InvalidOperationException(exceptionMessage);

            _mockEnv.Setup(e => e.EnvironmentName).Returns("Production");
            var middleware = new GlobalExceptionHandlerMiddleware(next, _mockLogger.Object, _mockEnv.Object);

            // Act
            await middleware.InvokeAsync(_httpContext);

            // Assert
            _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
            var responseBody = await new StreamReader(_httpContext.Response.Body).ReadToEndAsync();
            var errorResponse = JsonSerializer.Deserialize<ApiErrorResponse>(responseBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            Assert.That(errorResponse, Is.Not.Null);
            Assert.That(errorResponse!.Success, Is.False);
            Assert.That(errorResponse.ErrorCode, Is.Not.Null.And.Not.Empty);
            Assert.That(errorResponse.ErrorMessage, Is.Not.Null.And.Not.Empty);
            Assert.That(errorResponse.CorrelationId, Is.Not.Null);
        }

        [Test]
        public async Task InvokeAsync_SanitizesPathInLogging()
        {
            // Arrange
            var maliciousPath = "/api/test?password=secret&token=12345";
            _httpContext.Request.Path = maliciousPath;
            
            RequestDelegate next = (HttpContext hc) => throw new Exception("Test");

            _mockEnv.Setup(e => e.EnvironmentName).Returns("Production");
            var middleware = new GlobalExceptionHandlerMiddleware(next, _mockLogger.Object, _mockEnv.Object);

            // Act
            await middleware.InvokeAsync(_httpContext);

            // Assert - verify that the logged path doesn't contain query parameters
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("/api/test") && !v.ToString()!.Contains("password")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Test]
        public async Task InvokeAsync_SanitizesPathWithInjectionAttempt()
        {
            // Arrange - path with newline injection attempt
            var injectionPath = "/api/test\nINJECTED_LINE\r\nAnother line";
            _httpContext.Request.Path = injectionPath;
            
            RequestDelegate next = (HttpContext hc) => throw new Exception("Test");

            _mockEnv.Setup(e => e.EnvironmentName).Returns("Production");
            var middleware = new GlobalExceptionHandlerMiddleware(next, _mockLogger.Object, _mockEnv.Object);

            // Act
            await middleware.InvokeAsync(_httpContext);

            // Assert - verify that the logged path doesn't contain newlines
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => !v.ToString()!.Contains("\n") && !v.ToString()!.Contains("\r")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Test]
        public async Task InvokeAsync_ArgumentException_ReturnsBadRequest()
        {
            // Arrange
            RequestDelegate next = (HttpContext hc) => throw new ArgumentException("Invalid argument");

            _mockEnv.Setup(e => e.EnvironmentName).Returns("Production");
            var middleware = new GlobalExceptionHandlerMiddleware(next, _mockLogger.Object, _mockEnv.Object);

            // Act
            await middleware.InvokeAsync(_httpContext);

            // Assert
            Assert.That(_httpContext.Response.StatusCode, Is.EqualTo(400));
            
            _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
            var responseBody = await new StreamReader(_httpContext.Response.Body).ReadToEndAsync();
            var errorResponse = JsonSerializer.Deserialize<ApiErrorResponse>(responseBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            Assert.That(errorResponse!.ErrorCode, Is.EqualTo("BAD_REQUEST"));
        }

        [Test]
        public async Task InvokeAsync_TimeoutException_ReturnsRequestTimeout()
        {
            // Arrange
            RequestDelegate next = (HttpContext hc) => throw new TimeoutException("Request timed out");

            _mockEnv.Setup(e => e.EnvironmentName).Returns("Production");
            var middleware = new GlobalExceptionHandlerMiddleware(next, _mockLogger.Object, _mockEnv.Object);

            // Act
            await middleware.InvokeAsync(_httpContext);

            // Assert
            Assert.That(_httpContext.Response.StatusCode, Is.EqualTo(408));
            
            _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
            var responseBody = await new StreamReader(_httpContext.Response.Body).ReadToEndAsync();
            var errorResponse = JsonSerializer.Deserialize<ApiErrorResponse>(responseBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            Assert.That(errorResponse!.ErrorCode, Is.EqualTo("TIMEOUT"));
        }

        [Test]
        public async Task InvokeAsync_DevelopmentEnvironment_IncludesExceptionDetails()
        {
            // Arrange
            var exceptionMessage = "Detailed exception message";
            RequestDelegate next = (HttpContext hc) => throw new ArgumentException(exceptionMessage);

            // Mock EnvironmentName to be "Development" which makes IsDevelopment() return true
            _mockEnv.Setup(e => e.EnvironmentName).Returns(Environments.Development);
            var middleware = new GlobalExceptionHandlerMiddleware(next, _mockLogger.Object, _mockEnv.Object);

            // Act
            await middleware.InvokeAsync(_httpContext);

            // Assert
            _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
            var responseBody = await new StreamReader(_httpContext.Response.Body).ReadToEndAsync();
            var errorResponse = JsonSerializer.Deserialize<ApiErrorResponse>(responseBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            Assert.That(errorResponse!.Details, Is.Not.Null);
            Assert.That(errorResponse.Details!.ContainsKey("exceptionMessage"), Is.True);
        }

        [Test]
        public async Task InvokeAsync_ProductionEnvironment_DoesNotIncludeExceptionDetails()
        {
            // Arrange
            var exceptionMessage = "Sensitive exception message";
            RequestDelegate next = (HttpContext hc) => throw new ArgumentException(exceptionMessage);

            // Mock EnvironmentName to be "Production" which makes IsDevelopment() return false
            _mockEnv.Setup(e => e.EnvironmentName).Returns(Environments.Production);
            var middleware = new GlobalExceptionHandlerMiddleware(next, _mockLogger.Object, _mockEnv.Object);

            // Act
            await middleware.InvokeAsync(_httpContext);

            // Assert
            _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
            var responseBody = await new StreamReader(_httpContext.Response.Body).ReadToEndAsync();
            
            // Verify the response body doesn't contain the sensitive message
            Assert.That(responseBody.Contains(exceptionMessage), Is.False);
        }

        [Test]
        public async Task InvokeAsync_LongPath_TruncatesInResponse()
        {
            // Arrange
            var longPath = "/api/" + new string('a', 250); // 250+ character path
            _httpContext.Request.Path = longPath;
            
            RequestDelegate next = (HttpContext hc) => throw new Exception("Test");

            _mockEnv.Setup(e => e.EnvironmentName).Returns("Production");
            var middleware = new GlobalExceptionHandlerMiddleware(next, _mockLogger.Object, _mockEnv.Object);

            // Act
            await middleware.InvokeAsync(_httpContext);

            // Assert
            _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
            var responseBody = await new StreamReader(_httpContext.Response.Body).ReadToEndAsync();
            var errorResponse = JsonSerializer.Deserialize<ApiErrorResponse>(responseBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            // Path should be truncated to 200 chars + "..."
            Assert.That(errorResponse!.Path!.Length, Is.LessThanOrEqualTo(203));
            Assert.That(errorResponse.Path, Does.EndWith("..."));
        }

        [Test]
        public async Task InvokeAsync_SetsCorrectContentType()
        {
            // Arrange
            RequestDelegate next = (HttpContext hc) => throw new Exception("Test");

            _mockEnv.Setup(e => e.EnvironmentName).Returns("Production");
            var middleware = new GlobalExceptionHandlerMiddleware(next, _mockLogger.Object, _mockEnv.Object);

            // Act
            await middleware.InvokeAsync(_httpContext);

            // Assert
            Assert.That(_httpContext.Response.ContentType, Is.EqualTo("application/json"));
        }
    }
}

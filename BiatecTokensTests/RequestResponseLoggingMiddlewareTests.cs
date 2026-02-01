using BiatecTokensApi.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Unit tests for RequestResponseLoggingMiddleware
    /// Tests logging functionality, path sanitization, and performance tracking
    /// </summary>
    [TestFixture]
    public class RequestResponseLoggingMiddlewareTests
    {
        private Mock<ILogger<RequestResponseLoggingMiddleware>> _mockLogger = null!;
        private DefaultHttpContext _httpContext = null!;

        [SetUp]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger<RequestResponseLoggingMiddleware>>();
            _httpContext = new DefaultHttpContext();
            _httpContext.Response.Body = new MemoryStream();
        }

        [Test]
        public async Task InvokeAsync_LogsRequestAndResponse()
        {
            // Arrange
            _httpContext.Request.Method = "GET";
            _httpContext.Request.Path = "/api/test";
            
            RequestDelegate next = (HttpContext hc) =>
            {
                hc.Response.StatusCode = 200;
                return Task.CompletedTask;
            };

            var middleware = new RequestResponseLoggingMiddleware(next, _mockLogger.Object);

            // Act
            await middleware.InvokeAsync(_httpContext);

            // Assert - verify request log
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("HTTP Request") && v.ToString()!.Contains("GET")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            // Assert - verify response log
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("HTTP Response") && v.ToString()!.Contains("200")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Test]
        public async Task InvokeAsync_SanitizesPathInLogging()
        {
            // Arrange
            var pathWithQuery = "/api/users?password=secret123&token=abc";
            _httpContext.Request.Method = "POST";
            _httpContext.Request.Path = pathWithQuery;
            
            RequestDelegate next = (HttpContext hc) => Task.CompletedTask;

            var middleware = new RequestResponseLoggingMiddleware(next, _mockLogger.Object);

            // Act
            await middleware.InvokeAsync(_httpContext);

            // Assert - verify that logged path doesn't contain query parameters
            _mockLogger.Verify(
                x => x.Log(
                    It.IsAny<LogLevel>(),
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("/api/users") && !v.ToString()!.Contains("password")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        [Test]
        public async Task InvokeAsync_SanitizesPathWithNewlines()
        {
            // Arrange - path with newline injection attempt
            var injectionPath = "/api/test\nINJECTED\rLINE";
            _httpContext.Request.Method = "GET";
            _httpContext.Request.Path = injectionPath;
            
            RequestDelegate next = (HttpContext hc) => Task.CompletedTask;

            var middleware = new RequestResponseLoggingMiddleware(next, _mockLogger.Object);

            // Act
            await middleware.InvokeAsync(_httpContext);

            // Assert - verify no newlines in logged path
            _mockLogger.Verify(
                x => x.Log(
                    It.IsAny<LogLevel>(),
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => !v.ToString()!.Contains("\n") && !v.ToString()!.Contains("\r")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        [Test]
        public async Task InvokeAsync_LogsElapsedTime()
        {
            // Arrange
            _httpContext.Request.Method = "GET";
            _httpContext.Request.Path = "/api/test";
            
            RequestDelegate next = async (HttpContext hc) =>
            {
                await Task.Delay(10); // Simulate some processing time
            };

            var middleware = new RequestResponseLoggingMiddleware(next, _mockLogger.Object);

            // Act
            await middleware.InvokeAsync(_httpContext);

            // Assert - verify elapsed time is logged
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("ms")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        [Test]
        public async Task InvokeAsync_LogsCorrelationId()
        {
            // Arrange
            var correlationId = "test-correlation-id-12345";
            _httpContext.TraceIdentifier = correlationId;
            _httpContext.Request.Method = "GET";
            _httpContext.Request.Path = "/api/test";
            
            RequestDelegate next = (HttpContext hc) => Task.CompletedTask;

            var middleware = new RequestResponseLoggingMiddleware(next, _mockLogger.Object);

            // Act
            await middleware.InvokeAsync(_httpContext);

            // Assert - verify correlation ID is logged
            _mockLogger.Verify(
                x => x.Log(
                    It.IsAny<LogLevel>(),
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(correlationId)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        [Test]
        public async Task InvokeAsync_OnException_LogsError()
        {
            // Arrange
            var exceptionMessage = "Test exception";
            _httpContext.Request.Method = "POST";
            _httpContext.Request.Path = "/api/error";
            
            RequestDelegate next = (HttpContext hc) => throw new Exception(exceptionMessage);

            var middleware = new RequestResponseLoggingMiddleware(next, _mockLogger.Object);

            // Act & Assert
            Assert.ThrowsAsync<Exception>(async () => await middleware.InvokeAsync(_httpContext));

            // Verify error log
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("failed")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Test]
        public async Task InvokeAsync_OnException_RethrowsException()
        {
            // Arrange
            var expectedException = new InvalidOperationException("Test exception");
            _httpContext.Request.Method = "GET";
            _httpContext.Request.Path = "/api/test";
            
            RequestDelegate next = (HttpContext hc) => throw expectedException;

            var middleware = new RequestResponseLoggingMiddleware(next, _mockLogger.Object);

            // Act & Assert
            var actualException = Assert.ThrowsAsync<InvalidOperationException>(
                async () => await middleware.InvokeAsync(_httpContext));
            
            Assert.That(actualException, Is.SameAs(expectedException));
        }

        [Test]
        public async Task InvokeAsync_RestoresOriginalResponseStream()
        {
            // Arrange
            var originalStream = _httpContext.Response.Body;
            _httpContext.Request.Method = "GET";
            _httpContext.Request.Path = "/api/test";
            
            RequestDelegate next = (HttpContext hc) => Task.CompletedTask;

            var middleware = new RequestResponseLoggingMiddleware(next, _mockLogger.Object);

            // Act
            await middleware.InvokeAsync(_httpContext);

            // Assert - verify original stream is restored
            Assert.That(_httpContext.Response.Body, Is.SameAs(originalStream));
        }

        [Test]
        public async Task InvokeAsync_LongPath_TruncatesInLog()
        {
            // Arrange
            var longPath = "/api/" + new string('x', 250); // Very long path
            _httpContext.Request.Method = "GET";
            _httpContext.Request.Path = longPath;
            
            RequestDelegate next = (HttpContext hc) => Task.CompletedTask;

            var middleware = new RequestResponseLoggingMiddleware(next, _mockLogger.Object);

            // Act
            await middleware.InvokeAsync(_httpContext);

            // Assert - verify path is truncated with "..."
            _mockLogger.Verify(
                x => x.Log(
                    It.IsAny<LogLevel>(),
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("...")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        [Test]
        public async Task InvokeAsync_LogsHttpMethod()
        {
            // Arrange
            var methods = new[] { "GET", "POST", "PUT", "DELETE", "PATCH" };
            
            foreach (var method in methods)
            {
                _mockLogger.Reset();
                _httpContext = new DefaultHttpContext();
                _httpContext.Response.Body = new MemoryStream();
                _httpContext.Request.Method = method;
                _httpContext.Request.Path = "/api/test";
                
                RequestDelegate next = (HttpContext hc) => Task.CompletedTask;
                var middleware = new RequestResponseLoggingMiddleware(next, _mockLogger.Object);

                // Act
                await middleware.InvokeAsync(_httpContext);

                // Assert
                _mockLogger.Verify(
                    x => x.Log(
                        It.IsAny<LogLevel>(),
                        It.IsAny<EventId>(),
                        It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(method)),
                        It.IsAny<Exception>(),
                        It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                    Times.AtLeastOnce,
                    $"Method {method} should be logged");
            }
        }
    }
}

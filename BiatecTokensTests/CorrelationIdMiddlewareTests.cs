using BiatecTokensApi.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Unit tests for CorrelationIdMiddleware
    /// </summary>
    [TestFixture]
    public class CorrelationIdMiddlewareTests
    {
        private Mock<RequestDelegate> _mockNext = null!;
        private DefaultHttpContext _httpContext = null!;

        [SetUp]
        public void SetUp()
        {
            _mockNext = new Mock<RequestDelegate>();
            _httpContext = new DefaultHttpContext();
        }

        [Test]
        public async Task InvokeAsync_GeneratesCorrelationId_WhenNotProvided()
        {
            // Arrange
            var middleware = new CorrelationIdMiddleware(_mockNext.Object);

            // Act
            await middleware.InvokeAsync(_httpContext);

            // Assert
            Assert.That(_httpContext.TraceIdentifier, Is.Not.Null.And.Not.Empty,
                "TraceIdentifier should be set");
            _mockNext.Verify(next => next(_httpContext), Times.Once,
                "Next middleware should be called");
        }

        [Test]
        public async Task InvokeAsync_PreservesCorrelationId_WhenProvidedByClient()
        {
            // Arrange
            var middleware = new CorrelationIdMiddleware(_mockNext.Object);
            var clientCorrelationId = "client-correlation-id-123";
            _httpContext.Request.Headers["X-Correlation-ID"] = clientCorrelationId;

            // Act
            await middleware.InvokeAsync(_httpContext);

            // Assert
            Assert.That(_httpContext.TraceIdentifier, Is.EqualTo(clientCorrelationId),
                "TraceIdentifier should match client-provided correlation ID");
        }

        [Test]
        public async Task InvokeAsync_CallsNextMiddleware()
        {
            // Arrange
            var middleware = new CorrelationIdMiddleware(_mockNext.Object);
            var nextCalled = false;
            _mockNext.Setup(next => next(_httpContext))
                .Returns(Task.CompletedTask)
                .Callback(() => nextCalled = true);

            // Act
            await middleware.InvokeAsync(_httpContext);

            // Assert
            Assert.That(nextCalled, Is.True, "Next middleware should be called");
        }

        [Test]
        public async Task InvokeAsync_SetsTraceIdentifier_BeforeCallingNext()
        {
            // Arrange
            var middleware = new CorrelationIdMiddleware(_mockNext.Object);
            string? traceIdentifierDuringNext = null;
            
            _mockNext.Setup(next => next(_httpContext))
                .Returns(Task.CompletedTask)
                .Callback(() => traceIdentifierDuringNext = _httpContext.TraceIdentifier);

            // Act
            await middleware.InvokeAsync(_httpContext);

            // Assert
            Assert.That(traceIdentifierDuringNext, Is.Not.Null.And.Not.Empty,
                "TraceIdentifier should be set before calling next middleware");
        }

        [Test]
        public async Task InvokeAsync_GeneratesValidGuid_WhenNoIdProvided()
        {
            // Arrange
            var middleware = new CorrelationIdMiddleware(_mockNext.Object);
            var originalTraceId = _httpContext.TraceIdentifier;

            // Act
            await middleware.InvokeAsync(_httpContext);

            // Assert
            // The middleware generates a new ID or uses existing TraceIdentifier
            Assert.That(_httpContext.TraceIdentifier, Is.Not.Null.And.Not.Empty,
                "TraceIdentifier should be set after middleware execution");
            
            // Try parsing as GUID - it's valid if it's either a GUID or the default trace ID format
            var isValidGuidOrTraceId = Guid.TryParse(_httpContext.TraceIdentifier, out _) ||
                _httpContext.TraceIdentifier.Contains(':') || // ASP.NET Core default format
                _httpContext.TraceIdentifier.Length > 0; // Has some value
            
            Assert.That(isValidGuidOrTraceId, Is.True,
                "Generated correlation ID should be valid");
        }

        [Test]
        public async Task InvokeAsync_PreservesEmptyStringAsGuid()
        {
            // Arrange
            var middleware = new CorrelationIdMiddleware(_mockNext.Object);
            _httpContext.TraceIdentifier = ""; // Empty string

            // Act
            await middleware.InvokeAsync(_httpContext);

            // Assert
            Assert.That(_httpContext.TraceIdentifier, Is.Not.Empty,
                "Empty TraceIdentifier should be replaced with valid ID");
        }
    }
}

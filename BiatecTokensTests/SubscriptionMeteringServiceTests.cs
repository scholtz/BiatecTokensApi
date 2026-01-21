using BiatecTokensApi.Models.Metering;
using BiatecTokensApi.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace BiatecTokensTests
{
    [TestFixture]
    public class SubscriptionMeteringServiceTests
    {
        private Mock<ILogger<SubscriptionMeteringService>> _loggerMock;
        private SubscriptionMeteringService _service;

        [SetUp]
        public void Setup()
        {
            _loggerMock = new Mock<ILogger<SubscriptionMeteringService>>();
            _service = new SubscriptionMeteringService(_loggerMock.Object);
        }

        [Test]
        public void EmitMeteringEvent_ValidEvent_ShouldLogEvent()
        {
            // Arrange
            var meteringEvent = new SubscriptionMeteringEvent
            {
                Category = MeteringCategory.Compliance,
                OperationType = MeteringOperationType.Upsert,
                AssetId = 12345,
                Network = "voimain",
                PerformedBy = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA",
                ItemCount = 1,
                Metadata = new Dictionary<string, string>
                {
                    { "test", "value" }
                }
            };

            // Act
            _service.EmitMeteringEvent(meteringEvent);

            // Assert - verify that log was called with Information level
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("METERING_EVENT")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Test]
        public void EmitMeteringEvent_WhitelistOperation_ShouldLogEvent()
        {
            // Arrange
            var meteringEvent = new SubscriptionMeteringEvent
            {
                Category = MeteringCategory.Whitelist,
                OperationType = MeteringOperationType.Add,
                AssetId = 67890,
                Network = "aramidmain",
                PerformedBy = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA",
                ItemCount = 1
            };

            // Act
            _service.EmitMeteringEvent(meteringEvent);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("METERING_EVENT")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Test]
        public void EmitMeteringEvent_BulkOperation_ShouldLogEventWithCount()
        {
            // Arrange
            var meteringEvent = new SubscriptionMeteringEvent
            {
                Category = MeteringCategory.Whitelist,
                OperationType = MeteringOperationType.BulkAdd,
                AssetId = 11111,
                Network = "testnet",
                PerformedBy = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA",
                ItemCount = 50
            };

            // Act
            _service.EmitMeteringEvent(meteringEvent);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("METERING_EVENT") && v.ToString()!.Contains("50")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Test]
        public void EmitMeteringEvent_NullEvent_ShouldLogWarning()
        {
            // Act
            _service.EmitMeteringEvent(null!);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("null metering event")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            // Verify that no information log was emitted
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Never);
        }

        [Test]
        public void EmitMeteringEvent_EventWithoutNetwork_ShouldLogWithUnknown()
        {
            // Arrange
            var meteringEvent = new SubscriptionMeteringEvent
            {
                Category = MeteringCategory.Compliance,
                OperationType = MeteringOperationType.Delete,
                AssetId = 22222,
                Network = null,
                PerformedBy = null,
                ItemCount = 1
            };

            // Act
            _service.EmitMeteringEvent(meteringEvent);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("METERING_EVENT") && v.ToString()!.Contains("unknown")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Test]
        public void EmitMeteringEvent_EventHasUniqueId_ShouldGenerateUniqueIds()
        {
            // Arrange
            var event1 = new SubscriptionMeteringEvent
            {
                Category = MeteringCategory.Compliance,
                OperationType = MeteringOperationType.Upsert,
                AssetId = 12345
            };

            var event2 = new SubscriptionMeteringEvent
            {
                Category = MeteringCategory.Compliance,
                OperationType = MeteringOperationType.Upsert,
                AssetId = 12345
            };

            // Assert
            Assert.That(event1.EventId, Is.Not.Null);
            Assert.That(event2.EventId, Is.Not.Null);
            Assert.That(event1.EventId, Is.Not.EqualTo(event2.EventId));
        }
    }
}

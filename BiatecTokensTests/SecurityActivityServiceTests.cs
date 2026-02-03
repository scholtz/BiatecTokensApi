using BiatecTokensApi.Models;
using BiatecTokensApi.Repositories;
using BiatecTokensApi.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace BiatecTokensTests
{
    /// <summary>
    /// Unit tests for SecurityActivityService
    /// </summary>
    [TestFixture]
    public class SecurityActivityServiceTests
    {
        private Mock<ILogger<SecurityActivityService>> _mockLogger;
        private Mock<ILogger<SecurityActivityRepository>> _mockRepoLogger;

        [SetUp]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger<SecurityActivityService>>();
            _mockRepoLogger = new Mock<ILogger<SecurityActivityRepository>>();
        }

        [Test]
        public async Task LogEventAsync_ShouldLogSecurityEvent()
        {
            // Arrange
            var repository = new SecurityActivityRepository(_mockRepoLogger.Object);
            var deploymentStatusRepository = new Mock<BiatecTokensApi.Repositories.Interface.IDeploymentStatusRepository>();
            var service = new SecurityActivityService(repository, deploymentStatusRepository.Object, _mockLogger.Object);

            var securityEvent = new SecurityActivityEvent
            {
                AccountId = "TEST_ACCOUNT",
                EventType = SecurityEventType.Login,
                Severity = EventSeverity.Info,
                Summary = "User logged in successfully",
                Success = true
            };

            // Act
            await service.LogEventAsync(securityEvent);

            // Assert
            var request = new GetSecurityActivityRequest
            {
                AccountId = "TEST_ACCOUNT",
                Page = 1,
                PageSize = 10
            };
            var result = await service.GetActivityAsync(request);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Events, Has.Count.EqualTo(1));
            Assert.That(result.Events[0].AccountId, Is.EqualTo("TEST_ACCOUNT"));
            Assert.That(result.Events[0].EventType, Is.EqualTo(SecurityEventType.Login));
        }

        [Test]
        public async Task GetActivityAsync_WithFilters_ShouldReturnFilteredEvents()
        {
            // Arrange
            var repository = new SecurityActivityRepository(_mockRepoLogger.Object);
            var deploymentStatusRepository = new Mock<BiatecTokensApi.Repositories.Interface.IDeploymentStatusRepository>();
            var service = new SecurityActivityService(repository, deploymentStatusRepository.Object, _mockLogger.Object);

            // Add multiple events
            await service.LogEventAsync(new SecurityActivityEvent
            {
                AccountId = "USER1",
                EventType = SecurityEventType.Login,
                Severity = EventSeverity.Info,
                Success = true
            });

            await service.LogEventAsync(new SecurityActivityEvent
            {
                AccountId = "USER1",
                EventType = SecurityEventType.TokenDeploymentFailure,
                Severity = EventSeverity.Error,
                Success = false
            });

            await service.LogEventAsync(new SecurityActivityEvent
            {
                AccountId = "USER2",
                EventType = SecurityEventType.Login,
                Severity = EventSeverity.Info,
                Success = true
            });

            // Act - filter by account and event type
            var request = new GetSecurityActivityRequest
            {
                AccountId = "USER1",
                EventType = SecurityEventType.Login,
                Page = 1,
                PageSize = 10
            };
            var result = await service.GetActivityAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Events, Has.Count.EqualTo(1));
            Assert.That(result.Events[0].AccountId, Is.EqualTo("USER1"));
            Assert.That(result.Events[0].EventType, Is.EqualTo(SecurityEventType.Login));
        }

        [Test]
        public async Task GetActivityAsync_WithPagination_ShouldReturnPaginatedResults()
        {
            // Arrange
            var repository = new SecurityActivityRepository(_mockRepoLogger.Object);
            var deploymentStatusRepository = new Mock<BiatecTokensApi.Repositories.Interface.IDeploymentStatusRepository>();
            var service = new SecurityActivityService(repository, deploymentStatusRepository.Object, _mockLogger.Object);

            // Add multiple events
            for (int i = 0; i < 25; i++)
            {
                await service.LogEventAsync(new SecurityActivityEvent
                {
                    AccountId = "TEST_USER",
                    EventType = SecurityEventType.Login,
                    Severity = EventSeverity.Info,
                    Success = true
                });
            }

            // Act - get page 1
            var request1 = new GetSecurityActivityRequest
            {
                AccountId = "TEST_USER",
                Page = 1,
                PageSize = 10
            };
            var result1 = await service.GetActivityAsync(request1);

            // Act - get page 2
            var request2 = new GetSecurityActivityRequest
            {
                AccountId = "TEST_USER",
                Page = 2,
                PageSize = 10
            };
            var result2 = await service.GetActivityAsync(request2);

            // Assert
            Assert.That(result1.Success, Is.True);
            Assert.That(result1.Events.Count, Is.EqualTo(10));
            Assert.That(result1.TotalCount, Is.EqualTo(25));
            Assert.That(result1.TotalPages, Is.EqualTo(3));

            Assert.That(result2.Success, Is.True);
            Assert.That(result2.Events.Count, Is.EqualTo(10));
            Assert.That(result2.TotalCount, Is.EqualTo(25));
        }

        [Test]
        public async Task ExportAuditTrailAsync_CSV_ShouldGenerateValidCSV()
        {
            // Arrange
            var repository = new SecurityActivityRepository(_mockRepoLogger.Object);
            var deploymentStatusRepository = new Mock<BiatecTokensApi.Repositories.Interface.IDeploymentStatusRepository>();
            var service = new SecurityActivityService(repository, deploymentStatusRepository.Object, _mockLogger.Object);

            await service.LogEventAsync(new SecurityActivityEvent
            {
                AccountId = "TEST_USER",
                EventType = SecurityEventType.Login,
                Severity = EventSeverity.Info,
                Summary = "User login",
                Success = true
            });

            // Act
            var exportRequest = new ExportAuditTrailRequest
            {
                Format = "csv",
                AccountId = "TEST_USER"
            };
            var (response, content) = await service.ExportAuditTrailAsync(exportRequest, "TEST_USER");

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(content, Is.Not.Null);
            Assert.That(content, Does.Contain("EventId,AccountId,EventType"));
            Assert.That(content, Does.Contain("TEST_USER"));
            Assert.That(content, Does.Contain("Login"));
            Assert.That(response.RecordCount, Is.EqualTo(1));
        }

        [Test]
        public async Task ExportAuditTrailAsync_JSON_ShouldGenerateValidJSON()
        {
            // Arrange
            var repository = new SecurityActivityRepository(_mockRepoLogger.Object);
            var deploymentStatusRepository = new Mock<BiatecTokensApi.Repositories.Interface.IDeploymentStatusRepository>();
            var service = new SecurityActivityService(repository, deploymentStatusRepository.Object, _mockLogger.Object);

            await service.LogEventAsync(new SecurityActivityEvent
            {
                AccountId = "TEST_USER",
                EventType = SecurityEventType.TokenDeployment,
                Severity = EventSeverity.Info,
                Summary = "Token deployed",
                Success = true
            });

            // Act
            var exportRequest = new ExportAuditTrailRequest
            {
                Format = "json",
                AccountId = "TEST_USER"
            };
            var (response, content) = await service.ExportAuditTrailAsync(exportRequest, "TEST_USER");

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(content, Is.Not.Null);
            Assert.That(content, Does.Contain("\"exportedAt\""));
            Assert.That(content, Does.Contain("\"recordCount\""));
            Assert.That(content, Does.Contain("\"events\""));
            Assert.That(content, Does.Contain("TEST_USER"));
            Assert.That(response.RecordCount, Is.EqualTo(1));
        }

        [Test]
        public async Task ExportAuditTrailAsync_WithIdempotencyKey_ShouldCacheExport()
        {
            // Arrange
            var repository = new SecurityActivityRepository(_mockRepoLogger.Object);
            var deploymentStatusRepository = new Mock<BiatecTokensApi.Repositories.Interface.IDeploymentStatusRepository>();
            var service = new SecurityActivityService(repository, deploymentStatusRepository.Object, _mockLogger.Object);

            await service.LogEventAsync(new SecurityActivityEvent
            {
                AccountId = "TEST_USER",
                EventType = SecurityEventType.Login,
                Severity = EventSeverity.Info,
                Success = true
            });

            // Act - first export
            var exportRequest1 = new ExportAuditTrailRequest
            {
                Format = "json",
                AccountId = "TEST_USER",
                IdempotencyKey = "test-key-123"
            };
            var (response1, content1) = await service.ExportAuditTrailAsync(exportRequest1, "TEST_USER");

            // Act - second export with same key
            var exportRequest2 = new ExportAuditTrailRequest
            {
                Format = "json",
                AccountId = "TEST_USER",
                IdempotencyKey = "test-key-123"
            };
            var (response2, content2) = await service.ExportAuditTrailAsync(exportRequest2, "TEST_USER");

            // Assert
            Assert.That(response1.Success, Is.True);
            Assert.That(response1.IdempotencyHit, Is.False);
            Assert.That(content1, Is.Not.Null);

            Assert.That(response2.Success, Is.True);
            Assert.That(response2.IdempotencyHit, Is.True);
            Assert.That(content2, Is.Null); // Cached response doesn't include content
            Assert.That(response2.ExportId, Is.EqualTo(response1.ExportId));
        }

        [Test]
        public async Task ExportAuditTrailAsync_InvalidFormat_ShouldReturnError()
        {
            // Arrange
            var repository = new SecurityActivityRepository(_mockRepoLogger.Object);
            var deploymentStatusRepository = new Mock<BiatecTokensApi.Repositories.Interface.IDeploymentStatusRepository>();
            var service = new SecurityActivityService(repository, deploymentStatusRepository.Object, _mockLogger.Object);

            // Act
            var exportRequest = new ExportAuditTrailRequest
            {
                Format = "xml", // Invalid format
                AccountId = "TEST_USER"
            };
            var (response, content) = await service.ExportAuditTrailAsync(exportRequest, "TEST_USER");

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.ErrorCode, Is.EqualTo(ErrorCodes.INVALID_EXPORT_FORMAT));
            Assert.That(content, Is.Null);
        }

        [Test]
        public async Task GetRecoveryGuidanceAsync_ShouldReturnRecoverySteps()
        {
            // Arrange
            var repository = new SecurityActivityRepository(_mockRepoLogger.Object);
            var deploymentStatusRepository = new Mock<BiatecTokensApi.Repositories.Interface.IDeploymentStatusRepository>();
            var service = new SecurityActivityService(repository, deploymentStatusRepository.Object, _mockLogger.Object);

            // Act
            var result = await service.GetRecoveryGuidanceAsync("TEST_USER");

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Eligibility, Is.EqualTo(RecoveryEligibility.Eligible));
            Assert.That(result.CooldownRemaining, Is.EqualTo(0));
            Assert.That(result.Steps, Is.Not.Empty);
            Assert.That(result.Steps.Count, Is.EqualTo(4));
            Assert.That(result.Steps[0].Title, Is.EqualTo("Verify Identity"));
        }

        [Test]
        public async Task GetTransactionHistoryAsync_ShouldReturnTransactions()
        {
            // Arrange
            var repository = new SecurityActivityRepository(_mockRepoLogger.Object);
            var deploymentStatusRepository = new Mock<BiatecTokensApi.Repositories.Interface.IDeploymentStatusRepository>();
            var service = new SecurityActivityService(repository, deploymentStatusRepository.Object, _mockLogger.Object);

            // Add a transaction directly to repository
            var securityRepo = repository as SecurityActivityRepository;
            securityRepo?.LogTransaction(new TokenDeploymentTransaction
            {
                TransactionId = "TXN123",
                Network = "voimain-v1.0",
                TokenStandard = "ARC200",
                Status = "success",
                CreatorAddress = "TEST_USER"
            });

            // Act
            var request = new GetTransactionHistoryRequest
            {
                AccountId = "TEST_USER",
                Page = 1,
                PageSize = 10
            };
            var result = await service.GetTransactionHistoryAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Transactions, Has.Count.EqualTo(1));
            Assert.That(result.Transactions[0].TransactionId, Is.EqualTo("TXN123"));
            Assert.That(result.Transactions[0].Network, Is.EqualTo("voimain-v1.0"));
        }

        [Test]
        public async Task GetTransactionHistoryAsync_WithNetworkFilter_ShouldReturnFilteredTransactions()
        {
            // Arrange
            var repository = new SecurityActivityRepository(_mockRepoLogger.Object);
            var deploymentStatusRepository = new Mock<BiatecTokensApi.Repositories.Interface.IDeploymentStatusRepository>();
            var service = new SecurityActivityService(repository, deploymentStatusRepository.Object, _mockLogger.Object);

            var securityRepo = repository as SecurityActivityRepository;
            securityRepo?.LogTransaction(new TokenDeploymentTransaction
            {
                TransactionId = "TXN1",
                Network = "voimain-v1.0",
                TokenStandard = "ARC200",
                CreatorAddress = "TEST_USER"
            });
            securityRepo?.LogTransaction(new TokenDeploymentTransaction
            {
                TransactionId = "TXN2",
                Network = "mainnet-v1.0",
                TokenStandard = "ASA",
                CreatorAddress = "TEST_USER"
            });

            // Act
            var request = new GetTransactionHistoryRequest
            {
                AccountId = "TEST_USER",
                Network = "voimain-v1.0",
                Page = 1,
                PageSize = 10
            };
            var result = await service.GetTransactionHistoryAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Transactions, Has.Count.EqualTo(1));
            Assert.That(result.Transactions[0].TransactionId, Is.EqualTo("TXN1"));
            Assert.That(result.Transactions[0].Network, Is.EqualTo("voimain-v1.0"));
        }

        [Test]
        public async Task GetActivityAsync_WithSeverityFilter_ShouldReturnFilteredEvents()
        {
            // Arrange
            var repository = new SecurityActivityRepository(_mockRepoLogger.Object);
            var deploymentStatusRepository = new Mock<BiatecTokensApi.Repositories.Interface.IDeploymentStatusRepository>();
            var service = new SecurityActivityService(repository, deploymentStatusRepository.Object, _mockLogger.Object);

            await service.LogEventAsync(new SecurityActivityEvent
            {
                AccountId = "USER1",
                EventType = SecurityEventType.Login,
                Severity = EventSeverity.Info,
                Success = true
            });

            await service.LogEventAsync(new SecurityActivityEvent
            {
                AccountId = "USER1",
                EventType = SecurityEventType.LoginFailed,
                Severity = EventSeverity.Error,
                Success = false
            });

            // Act
            var request = new GetSecurityActivityRequest
            {
                AccountId = "USER1",
                Severity = EventSeverity.Error,
                Page = 1,
                PageSize = 10
            };
            var result = await service.GetActivityAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Events, Has.Count.EqualTo(1));
            Assert.That(result.Events[0].Severity, Is.EqualTo(EventSeverity.Error));
            Assert.That(result.Events[0].EventType, Is.EqualTo(SecurityEventType.LoginFailed));
        }

        [Test]
        public async Task GetActivityAsync_WithDateRangeFilter_ShouldReturnFilteredEvents()
        {
            // Arrange
            var repository = new SecurityActivityRepository(_mockRepoLogger.Object);
            var deploymentStatusRepository = new Mock<BiatecTokensApi.Repositories.Interface.IDeploymentStatusRepository>();
            var service = new SecurityActivityService(repository, deploymentStatusRepository.Object, _mockLogger.Object);

            var now = DateTime.UtcNow;
            var yesterday = now.AddDays(-1);
            var twoDaysAgo = now.AddDays(-2);

            await service.LogEventAsync(new SecurityActivityEvent
            {
                AccountId = "USER1",
                EventType = SecurityEventType.Login,
                Timestamp = twoDaysAgo,
                Success = true
            });

            await service.LogEventAsync(new SecurityActivityEvent
            {
                AccountId = "USER1",
                EventType = SecurityEventType.Login,
                Timestamp = now,
                Success = true
            });

            // Act
            var request = new GetSecurityActivityRequest
            {
                AccountId = "USER1",
                FromDate = yesterday,
                Page = 1,
                PageSize = 10
            };
            var result = await service.GetActivityAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Events, Has.Count.EqualTo(1));
            Assert.That(result.Events[0].Timestamp >= yesterday, Is.True);
        }
    }
}

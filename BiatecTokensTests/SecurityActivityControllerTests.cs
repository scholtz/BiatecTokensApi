using BiatecTokensApi.Controllers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Repositories;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;

namespace BiatecTokensTests
{
    /// <summary>
    /// Integration tests for SecurityActivityController
    /// </summary>
    [TestFixture]
    public class SecurityActivityControllerTests
    {
        private Mock<ILogger<SecurityActivityService>> _mockServiceLogger;
        private Mock<ILogger<SecurityActivityRepository>> _mockRepoLogger;
        private Mock<ILogger<SecurityActivityController>> _mockControllerLogger;

        [SetUp]
        public void Setup()
        {
            _mockServiceLogger = new Mock<ILogger<SecurityActivityService>>();
            _mockRepoLogger = new Mock<ILogger<SecurityActivityRepository>>();
            _mockControllerLogger = new Mock<ILogger<SecurityActivityController>>();
        }

        private SecurityActivityController CreateController(string userAddress = "TEST_USER")
        {
            var repository = new SecurityActivityRepository(_mockRepoLogger.Object);
            var deploymentStatusRepository = new Mock<BiatecTokensApi.Repositories.Interface.IDeploymentStatusRepository>();
            var service = new SecurityActivityService(repository, deploymentStatusRepository.Object, _mockServiceLogger.Object);
            var controller = new SecurityActivityController(service, _mockControllerLogger.Object);

            // Set up user context
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userAddress)
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var claimsPrincipal = new ClaimsPrincipal(identity);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = claimsPrincipal }
            };

            return controller;
        }

        [Test]
        public async Task GetActivity_ShouldReturnOk_WithSecurityEvents()
        {
            // Arrange
            var controller = CreateController("TEST_USER");
            
            // Log some events first
            var service = GetServiceFromController(controller);
            await service.LogEventAsync(new SecurityActivityEvent
            {
                AccountId = "TEST_USER",
                EventType = SecurityEventType.Login,
                Severity = EventSeverity.Info,
                Summary = "User login",
                Success = true
            });

            // Act
            var result = await controller.GetActivity();

            // Assert
            var okResult = result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);
            var response = okResult.Value as SecurityActivityResponse;
            Assert.That(response, Is.Not.Null);
            Assert.That(response.Success, Is.True);
            Assert.That(response.Events, Has.Count.EqualTo(1));
            Assert.That(response.Events[0].AccountId, Is.EqualTo("TEST_USER"));
        }

        [Test]
        public async Task GetActivity_WithFilters_ShouldReturnFilteredResults()
        {
            // Arrange
            var controller = CreateController("TEST_USER");
            var service = GetServiceFromController(controller);

            await service.LogEventAsync(new SecurityActivityEvent
            {
                AccountId = "TEST_USER",
                EventType = SecurityEventType.Login,
                Severity = EventSeverity.Info,
                Success = true
            });

            await service.LogEventAsync(new SecurityActivityEvent
            {
                AccountId = "TEST_USER",
                EventType = SecurityEventType.TokenDeploymentFailure,
                Severity = EventSeverity.Error,
                Success = false
            });

            // Act
            var result = await controller.GetActivity(
                eventType: SecurityEventType.TokenDeploymentFailure);

            // Assert
            var okResult = result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);
            var response = okResult.Value as SecurityActivityResponse;
            Assert.That(response, Is.Not.Null);
            Assert.That(response.Success, Is.True);
            Assert.That(response.Events, Has.Count.EqualTo(1));
            Assert.That(response.Events[0].EventType, Is.EqualTo(SecurityEventType.TokenDeploymentFailure));
        }

        [Test]
        public async Task GetActivity_WithPagination_ShouldReturnPaginatedResults()
        {
            // Arrange
            var controller = CreateController("TEST_USER");
            var service = GetServiceFromController(controller);

            // Add 15 events
            for (int i = 0; i < 15; i++)
            {
                await service.LogEventAsync(new SecurityActivityEvent
                {
                    AccountId = "TEST_USER",
                    EventType = SecurityEventType.Login,
                    Severity = EventSeverity.Info,
                    Success = true
                });
            }

            // Act - page 1
            var result1 = await controller.GetActivity(page: 1, pageSize: 10);
            var okResult1 = result1 as OkObjectResult;
            Assert.That(okResult1, Is.Not.Null);
            var response1 = okResult1.Value as SecurityActivityResponse;
            Assert.That(response1, Is.Not.Null);

            // Act - page 2
            var result2 = await controller.GetActivity(page: 2, pageSize: 10);
            var okResult2 = result2 as OkObjectResult;
            Assert.That(okResult2, Is.Not.Null);
            var response2 = okResult2.Value as SecurityActivityResponse;
            Assert.That(response2, Is.Not.Null);

            // Assert
            Assert.That(response1.Events.Count, Is.EqualTo(10));
            Assert.That(response1.TotalCount, Is.EqualTo(15));
            Assert.That(response1.TotalPages, Is.EqualTo(2));

            Assert.That(response2.Events.Count, Is.EqualTo(5));
            Assert.That(response2.TotalCount, Is.EqualTo(15));
        }

        [Test]
        public async Task GetTransactionHistory_ShouldReturnOk_WithTransactions()
        {
            // Arrange
            var controller = CreateController("TEST_USER");
            var service = GetServiceFromController(controller);
            var repository = GetRepositoryFromService(service);

            repository.LogTransaction(new TokenDeploymentTransaction
            {
                TransactionId = "TXN123",
                Network = "voimain-v1.0",
                TokenStandard = "ARC200",
                Status = "success",
                CreatorAddress = "TEST_USER"
            });

            // Act
            var result = await controller.GetTransactionHistory();

            // Assert
            var okResult = result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);
            var response = okResult.Value as TransactionHistoryResponse;
            Assert.That(response, Is.Not.Null);
            Assert.That(response.Success, Is.True);
            Assert.That(response.Transactions, Has.Count.EqualTo(1));
            Assert.That(response.Transactions[0].TransactionId, Is.EqualTo("TXN123"));
        }

        [Test]
        public async Task GetTransactionHistory_WithNetworkFilter_ShouldReturnFilteredResults()
        {
            // Arrange
            var controller = CreateController("TEST_USER");
            var service = GetServiceFromController(controller);
            var repository = GetRepositoryFromService(service);

            repository.LogTransaction(new TokenDeploymentTransaction
            {
                TransactionId = "TXN1",
                Network = "voimain-v1.0",
                TokenStandard = "ARC200",
                CreatorAddress = "TEST_USER"
            });

            repository.LogTransaction(new TokenDeploymentTransaction
            {
                TransactionId = "TXN2",
                Network = "mainnet-v1.0",
                TokenStandard = "ASA",
                CreatorAddress = "TEST_USER"
            });

            // Act
            var result = await controller.GetTransactionHistory(network: "voimain-v1.0");

            // Assert
            var okResult = result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);
            var response = okResult.Value as TransactionHistoryResponse;
            Assert.That(response, Is.Not.Null);
            Assert.That(response.Success, Is.True);
            Assert.That(response.Transactions, Has.Count.EqualTo(1));
            Assert.That(response.Transactions[0].Network, Is.EqualTo("voimain-v1.0"));
        }

        [Test]
        public async Task ExportAuditTrail_CSV_ShouldReturnFile()
        {
            // Arrange
            var controller = CreateController("TEST_USER");
            controller.ControllerContext.HttpContext.Request.Headers["Idempotency-Key"] = "test-key";
            
            var service = GetServiceFromController(controller);
            await service.LogEventAsync(new SecurityActivityEvent
            {
                AccountId = "TEST_USER",
                EventType = SecurityEventType.Login,
                Severity = EventSeverity.Info,
                Success = true
            });

            // Act
            var result = await controller.ExportAuditTrail(format: "csv");

            // Assert
            var fileResult = result as FileContentResult;
            Assert.That(fileResult, Is.Not.Null);
            Assert.That(fileResult.ContentType, Is.EqualTo("text/csv"));
            Assert.That(fileResult.FileDownloadName, Does.Contain("audit-trail-"));
            Assert.That(fileResult.FileDownloadName, Does.Contain(".csv"));
            
            var content = System.Text.Encoding.UTF8.GetString(fileResult.FileContents);
            Assert.That(content, Does.Contain("EventId,AccountId,EventType"));
            Assert.That(content, Does.Contain("TEST_USER"));
        }

        [Test]
        public async Task ExportAuditTrail_JSON_ShouldReturnFile()
        {
            // Arrange
            var controller = CreateController("TEST_USER");
            controller.ControllerContext.HttpContext.Request.Headers["Idempotency-Key"] = "test-key";
            
            var service = GetServiceFromController(controller);
            await service.LogEventAsync(new SecurityActivityEvent
            {
                AccountId = "TEST_USER",
                EventType = SecurityEventType.TokenDeployment,
                Severity = EventSeverity.Info,
                Success = true
            });

            // Act
            var result = await controller.ExportAuditTrail(format: "json");

            // Assert
            var fileResult = result as FileContentResult;
            Assert.That(fileResult, Is.Not.Null);
            Assert.That(fileResult.ContentType, Is.EqualTo("application/json"));
            Assert.That(fileResult.FileDownloadName, Does.Contain("audit-trail-"));
            Assert.That(fileResult.FileDownloadName, Does.Contain(".json"));
            
            var content = System.Text.Encoding.UTF8.GetString(fileResult.FileContents);
            Assert.That(content, Does.Contain("\"exportedAt\""));
            Assert.That(content, Does.Contain("\"events\""));
        }

        [Test]
        public async Task ExportAuditTrail_WithIdempotencyKey_ShouldReturnCachedResult()
        {
            // Arrange
            var controller = CreateController("TEST_USER");
            controller.ControllerContext.HttpContext.Request.Headers["Idempotency-Key"] = "idempotent-key-123";
            
            var service = GetServiceFromController(controller);
            await service.LogEventAsync(new SecurityActivityEvent
            {
                AccountId = "TEST_USER",
                EventType = SecurityEventType.Login,
                Success = true
            });

            // Act - first request
            var result1 = await controller.ExportAuditTrail(format: "json");
            var fileResult1 = result1 as FileContentResult;
            Assert.That(fileResult1, Is.Not.Null);

            // Act - second request with same key
            var result2 = await controller.ExportAuditTrail(format: "json");

            // Assert - second request should return cached response
            var okResult2 = result2 as OkObjectResult;
            Assert.That(okResult2, Is.Not.Null);
            var response2 = okResult2.Value as ExportAuditTrailResponse;
            Assert.That(response2, Is.Not.Null);
            Assert.That(response2.IdempotencyHit, Is.True);
            
            // Verify idempotency header was set
            Assert.That(controller.Response.Headers.ContainsKey("X-Idempotency-Hit"), Is.True);
        }

        [Test]
        public async Task ExportAuditTrail_InvalidFormat_ShouldReturnBadRequest()
        {
            // Arrange
            var controller = CreateController("TEST_USER");

            // Act
            var result = await controller.ExportAuditTrail(format: "xml");

            // Assert
            var badRequestResult = result as BadRequestObjectResult;
            Assert.That(badRequestResult, Is.Not.Null);
            var response = badRequestResult.Value as ExportAuditTrailResponse;
            Assert.That(response, Is.Not.Null);
            Assert.That(response.Success, Is.False);
            Assert.That(response.ErrorCode, Is.EqualTo(ErrorCodes.INVALID_EXPORT_FORMAT));
        }

        [Test]
        public async Task GetRecoveryGuidance_ShouldReturnOk_WithRecoverySteps()
        {
            // Arrange
            var controller = CreateController("TEST_USER");

            // Act
            var result = await controller.GetRecoveryGuidance();

            // Assert
            var okResult = result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);
            var response = okResult.Value as RecoveryGuidanceResponse;
            Assert.That(response, Is.Not.Null);
            Assert.That(response.Success, Is.True);
            Assert.That(response.Eligibility, Is.EqualTo(RecoveryEligibility.Eligible));
            Assert.That(response.Steps, Is.Not.Empty);
            Assert.That(response.Steps.Count, Is.EqualTo(4));
        }

        [Test]
        public async Task GetActivity_WithSeverityFilter_ShouldReturnFilteredResults()
        {
            // Arrange
            var controller = CreateController("TEST_USER");
            var service = GetServiceFromController(controller);

            await service.LogEventAsync(new SecurityActivityEvent
            {
                AccountId = "TEST_USER",
                EventType = SecurityEventType.Login,
                Severity = EventSeverity.Info,
                Success = true
            });

            await service.LogEventAsync(new SecurityActivityEvent
            {
                AccountId = "TEST_USER",
                EventType = SecurityEventType.LoginFailed,
                Severity = EventSeverity.Error,
                Success = false
            });

            // Act
            var result = await controller.GetActivity(severity: EventSeverity.Error);

            // Assert
            var okResult = result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);
            var response = okResult.Value as SecurityActivityResponse;
            Assert.That(response, Is.Not.Null);
            Assert.That(response.Success, Is.True);
            Assert.That(response.Events, Has.Count.EqualTo(1));
            Assert.That(response.Events[0].Severity, Is.EqualTo(EventSeverity.Error));
        }

        [Test]
        public async Task GetActivity_WithSuccessFilter_ShouldReturnFilteredResults()
        {
            // Arrange
            var controller = CreateController("TEST_USER");
            var service = GetServiceFromController(controller);

            await service.LogEventAsync(new SecurityActivityEvent
            {
                AccountId = "TEST_USER",
                EventType = SecurityEventType.Login,
                Success = true
            });

            await service.LogEventAsync(new SecurityActivityEvent
            {
                AccountId = "TEST_USER",
                EventType = SecurityEventType.LoginFailed,
                Success = false
            });

            // Act
            var result = await controller.GetActivity(success: false);

            // Assert
            var okResult = result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);
            var response = okResult.Value as SecurityActivityResponse;
            Assert.That(response, Is.Not.Null);
            Assert.That(response.Success, Is.True);
            Assert.That(response.Events, Has.Count.EqualTo(1));
            Assert.That(response.Events[0].Success, Is.False);
        }

        // Helper methods
        private ISecurityActivityService GetServiceFromController(SecurityActivityController controller)
        {
            var serviceField = controller.GetType().GetField("_securityActivityService",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return (ISecurityActivityService)serviceField!.GetValue(controller)!;
        }

        private SecurityActivityRepository GetRepositoryFromService(ISecurityActivityService service)
        {
            var repoField = service.GetType().GetField("_repository",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return (SecurityActivityRepository)repoField!.GetValue(service)!;
        }
    }
}

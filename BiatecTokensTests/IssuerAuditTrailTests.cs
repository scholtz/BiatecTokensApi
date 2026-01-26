using BiatecTokensApi.Controllers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System.Security.Claims;

namespace BiatecTokensTests
{
    /// <summary>
    /// Tests for RWA issuer compliance audit trail export endpoints
    /// </summary>
    [TestFixture]
    public class IssuerAuditTrailTests
    {
        private Mock<IComplianceService> _mockComplianceService;
        private Mock<IEnterpriseAuditService> _mockAuditService;
        private Mock<ILogger<IssuerController>> _mockLogger;
        private IssuerController _controller;

        private const string TestIssuerAddress = "ISSUER1234567890ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        private const string OtherIssuerAddress = "OTHER12345678901234567890123456789012345678";
        private const ulong TestAssetId = 12345;

        [SetUp]
        public void Setup()
        {
            _mockComplianceService = new Mock<IComplianceService>();
            _mockAuditService = new Mock<IEnterpriseAuditService>();
            _mockLogger = new Mock<ILogger<IssuerController>>();
            _controller = new IssuerController(
                _mockComplianceService.Object,
                _mockAuditService.Object,
                _mockLogger.Object);

            // Setup authentication context
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, TestIssuerAddress)
            };
            var identity = new ClaimsIdentity(claims, "Test");
            var claimsPrincipal = new ClaimsPrincipal(identity);
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = claimsPrincipal }
            };
        }

        #region GetIssuerAuditTrail Tests

        [Test]
        public async Task GetIssuerAuditTrail_ValidRequest_ReturnsAuditTrail()
        {
            // Arrange
            _mockComplianceService.Setup(s => s.VerifyIssuerOwnsAssetAsync(TestIssuerAddress, TestAssetId))
                .ReturnsAsync(true);

            var auditResponse = new EnterpriseAuditLogResponse
            {
                Success = true,
                Entries = new List<EnterpriseAuditLogEntry>
                {
                    new EnterpriseAuditLogEntry
                    {
                        Id = Guid.NewGuid().ToString(),
                        AssetId = TestAssetId,
                        Category = AuditEventCategory.TokenIssuance,
                        ActionType = "Create",
                        PerformedBy = TestIssuerAddress,
                        PerformedAt = DateTime.UtcNow,
                        Success = true
                    },
                    new EnterpriseAuditLogEntry
                    {
                        Id = Guid.NewGuid().ToString(),
                        AssetId = TestAssetId,
                        Category = AuditEventCategory.TransferValidation,
                        ActionType = "Validate",
                        PerformedBy = "SYSTEM",
                        PerformedAt = DateTime.UtcNow,
                        Success = true,
                        TransferAllowed = true,
                        ToAddress = "RECEIVER123456789012345678901234567890123456"
                    }
                },
                TotalCount = 2,
                Page = 1,
                PageSize = 50,
                TotalPages = 1
            };

            _mockAuditService.Setup(s => s.GetAuditLogAsync(It.IsAny<GetEnterpriseAuditLogRequest>()))
                .ReturnsAsync(auditResponse);

            // Act
            var result = await _controller.GetIssuerAuditTrail(assetId: TestAssetId);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            var response = okResult!.Value as EnterpriseAuditLogResponse;
            Assert.That(response, Is.Not.Null);
            Assert.That(response!.Success, Is.True);
            Assert.That(response.Entries, Has.Count.EqualTo(2));
            Assert.That(response.Entries[0].AssetId, Is.EqualTo(TestAssetId));
            Assert.That(response.Entries[1].Category, Is.EqualTo(AuditEventCategory.TransferValidation));
        }

        [Test]
        public async Task GetIssuerAuditTrail_MissingAssetId_ReturnsBadRequest()
        {
            // Act
            var result = await _controller.GetIssuerAuditTrail(assetId: null);

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
            var badRequest = result as BadRequestObjectResult;
            var response = badRequest!.Value as EnterpriseAuditLogResponse;
            Assert.That(response, Is.Not.Null);
            Assert.That(response!.Success, Is.False);
            Assert.That(response.ErrorMessage, Does.Contain("AssetId is required"));
        }

        [Test]
        public async Task GetIssuerAuditTrail_NotAssetOwner_ReturnsForbidden()
        {
            // Arrange - Issuer does not own the asset
            _mockComplianceService.Setup(s => s.VerifyIssuerOwnsAssetAsync(TestIssuerAddress, TestAssetId))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.GetIssuerAuditTrail(assetId: TestAssetId);

            // Assert
            Assert.That(result, Is.InstanceOf<ObjectResult>());
            var objectResult = result as ObjectResult;
            Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
            var response = objectResult.Value as EnterpriseAuditLogResponse;
            Assert.That(response, Is.Not.Null);
            Assert.That(response!.Success, Is.False);
            Assert.That(response.ErrorMessage, Does.Contain("not authorized"));
        }

        [Test]
        public async Task GetIssuerAuditTrail_WithDateRange_FiltersCorrectly()
        {
            // Arrange
            var fromDate = DateTime.UtcNow.AddDays(-30);
            var toDate = DateTime.UtcNow;

            _mockComplianceService.Setup(s => s.VerifyIssuerOwnsAssetAsync(TestIssuerAddress, TestAssetId))
                .ReturnsAsync(true);

            var auditResponse = new EnterpriseAuditLogResponse
            {
                Success = true,
                Entries = new List<EnterpriseAuditLogEntry>(),
                TotalCount = 0,
                Page = 1,
                PageSize = 50,
                TotalPages = 0
            };

            GetEnterpriseAuditLogRequest? capturedRequest = null;
            _mockAuditService.Setup(s => s.GetAuditLogAsync(It.IsAny<GetEnterpriseAuditLogRequest>()))
                .Callback<GetEnterpriseAuditLogRequest>(req => capturedRequest = req)
                .ReturnsAsync(auditResponse);

            // Act
            await _controller.GetIssuerAuditTrail(
                assetId: TestAssetId,
                fromDate: fromDate,
                toDate: toDate);

            // Assert
            Assert.That(capturedRequest, Is.Not.Null);
            Assert.That(capturedRequest!.AssetId, Is.EqualTo(TestAssetId));
            Assert.That(capturedRequest.FromDate, Is.EqualTo(fromDate));
            Assert.That(capturedRequest.ToDate, Is.EqualTo(toDate));
        }

        [Test]
        public async Task GetIssuerAuditTrail_WithActionType_FiltersCorrectly()
        {
            // Arrange
            const string actionType = "TransferValidation";

            _mockComplianceService.Setup(s => s.VerifyIssuerOwnsAssetAsync(TestIssuerAddress, TestAssetId))
                .ReturnsAsync(true);

            var auditResponse = new EnterpriseAuditLogResponse
            {
                Success = true,
                Entries = new List<EnterpriseAuditLogEntry>
                {
                    new EnterpriseAuditLogEntry
                    {
                        Id = Guid.NewGuid().ToString(),
                        AssetId = TestAssetId,
                        Category = AuditEventCategory.TransferValidation,
                        ActionType = actionType,
                        PerformedBy = "SYSTEM",
                        PerformedAt = DateTime.UtcNow,
                        Success = true,
                        TransferAllowed = false,
                        DenialReason = "Sender not whitelisted"
                    }
                },
                TotalCount = 1,
                Page = 1,
                PageSize = 50,
                TotalPages = 1
            };

            GetEnterpriseAuditLogRequest? capturedRequest = null;
            _mockAuditService.Setup(s => s.GetAuditLogAsync(It.IsAny<GetEnterpriseAuditLogRequest>()))
                .Callback<GetEnterpriseAuditLogRequest>(req => capturedRequest = req)
                .ReturnsAsync(auditResponse);

            // Act
            var result = await _controller.GetIssuerAuditTrail(
                assetId: TestAssetId,
                actionType: actionType);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            Assert.That(capturedRequest, Is.Not.Null);
            Assert.That(capturedRequest!.ActionType, Is.EqualTo(actionType));
            
            var okResult = result as OkObjectResult;
            var response = okResult!.Value as EnterpriseAuditLogResponse;
            Assert.That(response!.Entries, Has.Count.EqualTo(1));
            Assert.That(response.Entries[0].ActionType, Is.EqualTo(actionType));
            Assert.That(response.Entries[0].TransferAllowed, Is.False);
            Assert.That(response.Entries[0].DenialReason, Does.Contain("not whitelisted"));
        }

        [Test]
        public async Task GetIssuerAuditTrail_WithPagination_ReturnsCorrectPage()
        {
            // Arrange
            const int page = 2;
            const int pageSize = 25;

            _mockComplianceService.Setup(s => s.VerifyIssuerOwnsAssetAsync(TestIssuerAddress, TestAssetId))
                .ReturnsAsync(true);

            var auditResponse = new EnterpriseAuditLogResponse
            {
                Success = true,
                Entries = new List<EnterpriseAuditLogEntry>(),
                TotalCount = 100,
                Page = page,
                PageSize = pageSize,
                TotalPages = 4
            };

            GetEnterpriseAuditLogRequest? capturedRequest = null;
            _mockAuditService.Setup(s => s.GetAuditLogAsync(It.IsAny<GetEnterpriseAuditLogRequest>()))
                .Callback<GetEnterpriseAuditLogRequest>(req => capturedRequest = req)
                .ReturnsAsync(auditResponse);

            // Act
            await _controller.GetIssuerAuditTrail(
                assetId: TestAssetId,
                page: page,
                pageSize: pageSize);

            // Assert
            Assert.That(capturedRequest, Is.Not.Null);
            Assert.That(capturedRequest!.Page, Is.EqualTo(page));
            Assert.That(capturedRequest.PageSize, Is.EqualTo(pageSize));
        }

        #endregion

        #region ExportIssuerAuditTrailCsv Tests

        [Test]
        public async Task ExportIssuerAuditTrailCsv_ValidRequest_ReturnsCsvFile()
        {
            // Arrange
            _mockComplianceService.Setup(s => s.VerifyIssuerOwnsAssetAsync(TestIssuerAddress, TestAssetId))
                .ReturnsAsync(true);

            var csvContent = "Id,AssetId,Network,Category,ActionType,PerformedBy,PerformedAt,Success\n" +
                           $"1,{TestAssetId},voimain-v1.0,TokenIssuance,Create,{TestIssuerAddress},2024-01-01T00:00:00Z,True";

            _mockAuditService.Setup(s => s.ExportAuditLogCsvAsync(It.IsAny<GetEnterpriseAuditLogRequest>(), It.IsAny<int>()))
                .ReturnsAsync(csvContent);

            // Act
            var result = await _controller.ExportIssuerAuditTrailCsv(assetId: TestAssetId);

            // Assert
            Assert.That(result, Is.InstanceOf<FileContentResult>());
            var fileResult = result as FileContentResult;
            Assert.That(fileResult, Is.Not.Null);
            Assert.That(fileResult!.ContentType, Is.EqualTo("text/csv"));
            Assert.That(fileResult.FileDownloadName, Does.Contain($"issuer-audit-trail-{TestAssetId}"));
            Assert.That(fileResult.FileDownloadName, Does.EndWith(".csv"));
        }

        [Test]
        public async Task ExportIssuerAuditTrailCsv_MissingAssetId_ReturnsBadRequest()
        {
            // Act
            var result = await _controller.ExportIssuerAuditTrailCsv(assetId: null);

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task ExportIssuerAuditTrailCsv_NotAssetOwner_ReturnsForbidden()
        {
            // Arrange
            _mockComplianceService.Setup(s => s.VerifyIssuerOwnsAssetAsync(TestIssuerAddress, TestAssetId))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.ExportIssuerAuditTrailCsv(assetId: TestAssetId);

            // Assert
            Assert.That(result, Is.InstanceOf<ObjectResult>());
            var objectResult = result as ObjectResult;
            Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
        }

        [Test]
        public async Task ExportIssuerAuditTrailCsv_WithFilters_AppliesFilters()
        {
            // Arrange
            var fromDate = DateTime.UtcNow.AddDays(-7);
            var toDate = DateTime.UtcNow;
            const string actionType = "TransferValidation";

            _mockComplianceService.Setup(s => s.VerifyIssuerOwnsAssetAsync(TestIssuerAddress, TestAssetId))
                .ReturnsAsync(true);

            GetEnterpriseAuditLogRequest? capturedRequest = null;
            _mockAuditService.Setup(s => s.ExportAuditLogCsvAsync(It.IsAny<GetEnterpriseAuditLogRequest>(), It.IsAny<int>()))
                .Callback<GetEnterpriseAuditLogRequest, int>((req, max) => capturedRequest = req)
                .ReturnsAsync("CSV content");

            // Act
            await _controller.ExportIssuerAuditTrailCsv(
                assetId: TestAssetId,
                fromDate: fromDate,
                toDate: toDate,
                actionType: actionType);

            // Assert
            Assert.That(capturedRequest, Is.Not.Null);
            Assert.That(capturedRequest!.AssetId, Is.EqualTo(TestAssetId));
            Assert.That(capturedRequest.FromDate, Is.EqualTo(fromDate));
            Assert.That(capturedRequest.ToDate, Is.EqualTo(toDate));
            Assert.That(capturedRequest.ActionType, Is.EqualTo(actionType));
        }

        #endregion

        #region ExportIssuerAuditTrailJson Tests

        [Test]
        public async Task ExportIssuerAuditTrailJson_ValidRequest_ReturnsJsonFile()
        {
            // Arrange
            _mockComplianceService.Setup(s => s.VerifyIssuerOwnsAssetAsync(TestIssuerAddress, TestAssetId))
                .ReturnsAsync(true);

            var jsonContent = "{\"success\":true,\"entries\":[]}";

            _mockAuditService.Setup(s => s.ExportAuditLogJsonAsync(It.IsAny<GetEnterpriseAuditLogRequest>(), It.IsAny<int>()))
                .ReturnsAsync(jsonContent);

            // Act
            var result = await _controller.ExportIssuerAuditTrailJson(assetId: TestAssetId);

            // Assert
            Assert.That(result, Is.InstanceOf<FileContentResult>());
            var fileResult = result as FileContentResult;
            Assert.That(fileResult, Is.Not.Null);
            Assert.That(fileResult!.ContentType, Is.EqualTo("application/json"));
            Assert.That(fileResult.FileDownloadName, Does.Contain($"issuer-audit-trail-{TestAssetId}"));
            Assert.That(fileResult.FileDownloadName, Does.EndWith(".json"));
        }

        [Test]
        public async Task ExportIssuerAuditTrailJson_MissingAssetId_ReturnsBadRequest()
        {
            // Act
            var result = await _controller.ExportIssuerAuditTrailJson(assetId: null);

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task ExportIssuerAuditTrailJson_NotAssetOwner_ReturnsForbidden()
        {
            // Arrange
            _mockComplianceService.Setup(s => s.VerifyIssuerOwnsAssetAsync(TestIssuerAddress, TestAssetId))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.ExportIssuerAuditTrailJson(assetId: TestAssetId);

            // Assert
            Assert.That(result, Is.InstanceOf<ObjectResult>());
            var objectResult = result as ObjectResult;
            Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
        }

        [Test]
        public async Task ExportIssuerAuditTrailJson_WithFilters_AppliesFilters()
        {
            // Arrange
            var fromDate = DateTime.UtcNow.AddDays(-30);
            var toDate = DateTime.UtcNow;
            const string actionType = "Mint";

            _mockComplianceService.Setup(s => s.VerifyIssuerOwnsAssetAsync(TestIssuerAddress, TestAssetId))
                .ReturnsAsync(true);

            GetEnterpriseAuditLogRequest? capturedRequest = null;
            _mockAuditService.Setup(s => s.ExportAuditLogJsonAsync(It.IsAny<GetEnterpriseAuditLogRequest>(), It.IsAny<int>()))
                .Callback<GetEnterpriseAuditLogRequest, int>((req, max) => capturedRequest = req)
                .ReturnsAsync("{}");

            // Act
            await _controller.ExportIssuerAuditTrailJson(
                assetId: TestAssetId,
                fromDate: fromDate,
                toDate: toDate,
                actionType: actionType);

            // Assert
            Assert.That(capturedRequest, Is.Not.Null);
            Assert.That(capturedRequest!.AssetId, Is.EqualTo(TestAssetId));
            Assert.That(capturedRequest.FromDate, Is.EqualTo(fromDate));
            Assert.That(capturedRequest.ToDate, Is.EqualTo(toDate));
            Assert.That(capturedRequest.ActionType, Is.EqualTo(actionType));
        }

        #endregion

        #region Whitelist Enforcement Inclusion Tests

        [Test]
        public async Task GetIssuerAuditTrail_IncludesWhitelistEnforcementEvents()
        {
            // Arrange
            _mockComplianceService.Setup(s => s.VerifyIssuerOwnsAssetAsync(TestIssuerAddress, TestAssetId))
                .ReturnsAsync(true);

            var auditResponse = new EnterpriseAuditLogResponse
            {
                Success = true,
                Entries = new List<EnterpriseAuditLogEntry>
                {
                    new EnterpriseAuditLogEntry
                    {
                        Id = Guid.NewGuid().ToString(),
                        AssetId = TestAssetId,
                        Category = AuditEventCategory.TransferValidation,
                        ActionType = "ValidateTransfer",
                        PerformedBy = "SYSTEM",
                        PerformedAt = DateTime.UtcNow,
                        Success = true,
                        TransferAllowed = false,
                        DenialReason = "Receiver not on whitelist",
                        AffectedAddress = "SENDER1234567890123456789012345678901234567",
                        ToAddress = "RECEIVER12345678901234567890123456789012345",
                        Amount = 1000
                    }
                },
                TotalCount = 1,
                Page = 1,
                PageSize = 50,
                TotalPages = 1
            };

            _mockAuditService.Setup(s => s.GetAuditLogAsync(It.IsAny<GetEnterpriseAuditLogRequest>()))
                .ReturnsAsync(auditResponse);

            // Act
            var result = await _controller.GetIssuerAuditTrail(assetId: TestAssetId);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            var response = okResult!.Value as EnterpriseAuditLogResponse;
            Assert.That(response, Is.Not.Null);
            Assert.That(response!.Entries, Has.Count.EqualTo(1));
            
            var entry = response.Entries[0];
            Assert.That(entry.Category, Is.EqualTo(AuditEventCategory.TransferValidation));
            Assert.That(entry.TransferAllowed, Is.False);
            Assert.That(entry.DenialReason, Does.Contain("whitelist"));
            Assert.That(entry.ToAddress, Is.Not.Null);
            Assert.That(entry.Amount, Is.EqualTo(1000));
        }

        #endregion
    }
}

using BiatecTokensApi.Controllers;
using BiatecTokensApi.Models.Whitelist;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;

namespace BiatecTokensTests
{
    [TestFixture]
    public class WhitelistControllerTests
    {
        private Mock<IWhitelistService> _whitelistServiceMock;
        private Mock<ILogger<WhitelistController>> _loggerMock;
        private WhitelistController _controller;
        private const string TestUserAddress = "TESTUSER1234567890123456789012345678901234567890123456";

        [SetUp]
        public void Setup()
        {
            _whitelistServiceMock = new Mock<IWhitelistService>();
            _loggerMock = new Mock<ILogger<WhitelistController>>();
            _controller = new WhitelistController(_whitelistServiceMock.Object, _loggerMock.Object);

            // Set up authenticated user context
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, TestUserAddress),
                new Claim("sub", TestUserAddress)
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            };
        }

        #region ListWhitelist Tests

        [Test]
        public async Task ListWhitelist_ValidRequest_ShouldReturnOk()
        {
            // Arrange
            var assetId = 12345UL;
            var response = new WhitelistListResponse
            {
                Success = true,
                Entries = new List<WhitelistEntry>
                {
                    new WhitelistEntry { AssetId = assetId, Address = "ADDR1", Status = WhitelistStatus.Active }
                },
                TotalCount = 1,
                Page = 1,
                PageSize = 20,
                TotalPages = 1
            };

            _whitelistServiceMock.Setup(s => s.ListEntriesAsync(It.IsAny<ListWhitelistRequest>()))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.ListWhitelist(assetId);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            Assert.That(okResult?.Value, Is.InstanceOf<WhitelistListResponse>());
            var listResponse = okResult?.Value as WhitelistListResponse;
            Assert.That(listResponse?.Success, Is.True);
            Assert.That(listResponse?.Entries, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task ListWhitelist_ServiceFailure_ShouldReturnInternalServerError()
        {
            // Arrange
            var assetId = 12345UL;
            var response = new WhitelistListResponse
            {
                Success = false,
                ErrorMessage = "Internal error"
            };

            _whitelistServiceMock.Setup(s => s.ListEntriesAsync(It.IsAny<ListWhitelistRequest>()))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.ListWhitelist(assetId);

            // Assert
            Assert.That(result, Is.InstanceOf<ObjectResult>());
            var objectResult = result as ObjectResult;
            Assert.That(objectResult?.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
        }

        [Test]
        public async Task ListWhitelist_WithPagination_ShouldPassCorrectParameters()
        {
            // Arrange
            var assetId = 12345UL;
            var page = 2;
            var pageSize = 50;
            var status = WhitelistStatus.Active;

            _whitelistServiceMock.Setup(s => s.ListEntriesAsync(It.Is<ListWhitelistRequest>(r =>
                r.AssetId == assetId &&
                r.Page == page &&
                r.PageSize == pageSize &&
                r.Status == status
            ))).ReturnsAsync(new WhitelistListResponse { Success = true });

            // Act
            var result = await _controller.ListWhitelist(assetId, status, page, pageSize);

            // Assert
            _whitelistServiceMock.Verify(s => s.ListEntriesAsync(It.Is<ListWhitelistRequest>(r =>
                r.AssetId == assetId &&
                r.Page == page &&
                r.PageSize == pageSize &&
                r.Status == status
            )), Times.Once);
        }

        [Test]
        public async Task ListWhitelist_PageSizeOver100_ShouldCapAt100()
        {
            // Arrange
            var assetId = 12345UL;
            var pageSize = 150;

            _whitelistServiceMock.Setup(s => s.ListEntriesAsync(It.Is<ListWhitelistRequest>(r =>
                r.PageSize == 100
            ))).ReturnsAsync(new WhitelistListResponse { Success = true });

            // Act
            var result = await _controller.ListWhitelist(assetId, null, 1, pageSize);

            // Assert
            _whitelistServiceMock.Verify(s => s.ListEntriesAsync(It.Is<ListWhitelistRequest>(r =>
                r.PageSize == 100
            )), Times.Once);
        }

        #endregion

        #region AddWhitelistEntry Tests

        [Test]
        public async Task AddWhitelistEntry_ValidRequest_ShouldReturnOk()
        {
            // Arrange
            var request = new AddWhitelistEntryRequest
            {
                AssetId = 12345,
                Address = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ",
                Status = WhitelistStatus.Active
            };

            var response = new WhitelistResponse
            {
                Success = true,
                Entry = new WhitelistEntry
                {
                    AssetId = request.AssetId,
                    Address = request.Address,
                    Status = request.Status,
                    CreatedBy = TestUserAddress
                }
            };

            _whitelistServiceMock.Setup(s => s.AddEntryAsync(It.IsAny<AddWhitelistEntryRequest>(), TestUserAddress))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.AddWhitelistEntry(request);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            Assert.That(okResult?.Value, Is.InstanceOf<WhitelistResponse>());
            var addResponse = okResult?.Value as WhitelistResponse;
            Assert.That(addResponse?.Success, Is.True);
        }

        [Test]
        public async Task AddWhitelistEntry_InvalidModelState_ShouldReturnBadRequest()
        {
            // Arrange
            var request = new AddWhitelistEntryRequest
            {
                AssetId = 12345,
                Address = "INVALID"
            };
            _controller.ModelState.AddModelError("Address", "Invalid address");

            // Act
            var result = await _controller.AddWhitelistEntry(request);

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task AddWhitelistEntry_ServiceFailure_ShouldReturnBadRequest()
        {
            // Arrange
            var request = new AddWhitelistEntryRequest
            {
                AssetId = 12345,
                Address = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ"
            };

            var response = new WhitelistResponse
            {
                Success = false,
                ErrorMessage = "Invalid address format"
            };

            _whitelistServiceMock.Setup(s => s.AddEntryAsync(It.IsAny<AddWhitelistEntryRequest>(), TestUserAddress))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.AddWhitelistEntry(request);

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        }

        #endregion

        #region RemoveWhitelistEntry Tests

        [Test]
        public async Task RemoveWhitelistEntry_ValidRequest_ShouldReturnOk()
        {
            // Arrange
            var request = new RemoveWhitelistEntryRequest
            {
                AssetId = 12345,
                Address = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ"
            };

            var response = new WhitelistResponse
            {
                Success = true
            };

            _whitelistServiceMock.Setup(s => s.RemoveEntryAsync(It.IsAny<RemoveWhitelistEntryRequest>()))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.RemoveWhitelistEntry(request);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
        }

        [Test]
        public async Task RemoveWhitelistEntry_InvalidModelState_ShouldReturnBadRequest()
        {
            // Arrange
            var request = new RemoveWhitelistEntryRequest
            {
                AssetId = 12345,
                Address = "INVALID"
            };
            _controller.ModelState.AddModelError("Address", "Invalid address");

            // Act
            var result = await _controller.RemoveWhitelistEntry(request);

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task RemoveWhitelistEntry_EntryNotFound_ShouldReturnNotFound()
        {
            // Arrange
            var request = new RemoveWhitelistEntryRequest
            {
                AssetId = 12345,
                Address = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ"
            };

            var response = new WhitelistResponse
            {
                Success = false,
                ErrorMessage = "Whitelist entry not found"
            };

            _whitelistServiceMock.Setup(s => s.RemoveEntryAsync(It.IsAny<RemoveWhitelistEntryRequest>()))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.RemoveWhitelistEntry(request);

            // Assert
            Assert.That(result, Is.InstanceOf<NotFoundObjectResult>());
        }

        #endregion

        #region BulkAddWhitelistEntries Tests

        [Test]
        public async Task BulkAddWhitelistEntries_ValidRequest_ShouldReturnOk()
        {
            // Arrange
            var request = new BulkAddWhitelistRequest
            {
                AssetId = 12345,
                Addresses = new List<string>
                {
                    "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ",
                    "7Z5PWO2C6KKEF7HIQFFQTQVDPBJBEU6QGJE4PZSJFWSNLNRPRQFKJGUVAQ"
                }
            };

            var response = new BulkWhitelistResponse
            {
                Success = true,
                SuccessCount = 2,
                FailedCount = 0
            };

            _whitelistServiceMock.Setup(s => s.BulkAddEntriesAsync(It.IsAny<BulkAddWhitelistRequest>(), TestUserAddress))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.BulkAddWhitelistEntries(request);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            Assert.That(okResult?.Value, Is.InstanceOf<BulkWhitelistResponse>());
            var bulkResponse = okResult?.Value as BulkWhitelistResponse;
            Assert.That(bulkResponse?.SuccessCount, Is.EqualTo(2));
        }

        [Test]
        public async Task BulkAddWhitelistEntries_InvalidModelState_ShouldReturnBadRequest()
        {
            // Arrange
            var request = new BulkAddWhitelistRequest
            {
                AssetId = 12345,
                Addresses = new List<string>()
            };
            _controller.ModelState.AddModelError("Addresses", "At least one address is required");

            // Act
            var result = await _controller.BulkAddWhitelistEntries(request);

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task BulkAddWhitelistEntries_PartialSuccess_ShouldReturnOkWithDetails()
        {
            // Arrange
            var request = new BulkAddWhitelistRequest
            {
                AssetId = 12345,
                Addresses = new List<string>
                {
                    "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ",
                    "INVALID"
                }
            };

            var response = new BulkWhitelistResponse
            {
                Success = false,
                SuccessCount = 1,
                FailedCount = 1,
                FailedAddresses = new List<string> { "INVALID" },
                ValidationErrors = new List<string> { "Invalid address format: INVALID" }
            };

            _whitelistServiceMock.Setup(s => s.BulkAddEntriesAsync(It.IsAny<BulkAddWhitelistRequest>(), TestUserAddress))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.BulkAddWhitelistEntries(request);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            var bulkResponse = okResult?.Value as BulkWhitelistResponse;
            Assert.That(bulkResponse?.SuccessCount, Is.EqualTo(1));
            Assert.That(bulkResponse?.FailedCount, Is.EqualTo(1));
        }

        #endregion

        #region Authorization Tests

        [Test]
        public async Task AddWhitelistEntry_NoUserInContext_ShouldReturnUnauthorized()
        {
            // Arrange
            var request = new AddWhitelistEntryRequest
            {
                AssetId = 12345,
                Address = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ"
            };

            // Set up controller with no user context
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
            };

            // Act
            var result = await _controller.AddWhitelistEntry(request);

            // Assert
            Assert.That(result, Is.InstanceOf<UnauthorizedObjectResult>());
        }

        [Test]
        public async Task RemoveWhitelistEntry_NoUserInContext_ShouldReturnUnauthorized()
        {
            // Arrange
            var request = new RemoveWhitelistEntryRequest
            {
                AssetId = 12345,
                Address = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ"
            };

            // Set up controller with no user context
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
            };

            // Act
            var result = await _controller.RemoveWhitelistEntry(request);

            // Assert
            Assert.That(result, Is.InstanceOf<UnauthorizedObjectResult>());
        }

        [Test]
        public async Task BulkAddWhitelistEntries_NoUserInContext_ShouldReturnUnauthorized()
        {
            // Arrange
            var request = new BulkAddWhitelistRequest
            {
                AssetId = 12345,
                Addresses = new List<string> { "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ" }
            };

            // Set up controller with no user context
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
            };

            // Act
            var result = await _controller.BulkAddWhitelistEntries(request);

            // Assert
            Assert.That(result, Is.InstanceOf<UnauthorizedObjectResult>());
        }

        #endregion

        #region GetAuditLog Tests

        [Test]
        public async Task GetAuditLog_ValidRequest_ShouldReturnOk()
        {
            // Arrange
            var assetId = 12345UL;
            var response = new WhitelistAuditLogResponse
            {
                Success = true,
                Entries = new List<WhitelistAuditLogEntry>
                {
                    new WhitelistAuditLogEntry
                    {
                        AssetId = assetId,
                        Address = "ADDRESS1123456789012345678901234567890123456789012345",
                        ActionType = WhitelistActionType.Add,
                        PerformedBy = TestUserAddress,
                        PerformedAt = DateTime.UtcNow,
                        NewStatus = WhitelistStatus.Active
                    }
                },
                TotalCount = 1,
                Page = 1,
                PageSize = 50,
                TotalPages = 1
            };

            _whitelistServiceMock
                .Setup(s => s.GetAuditLogAsync(It.IsAny<GetWhitelistAuditLogRequest>()))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.GetAuditLog(assetId);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            Assert.That(okResult?.Value, Is.InstanceOf<WhitelistAuditLogResponse>());
            var auditResponse = okResult?.Value as WhitelistAuditLogResponse;
            Assert.That(auditResponse?.Success, Is.True);
            Assert.That(auditResponse?.Entries, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task GetAuditLog_WithFilters_ShouldPassFiltersToService()
        {
            // Arrange
            var assetId = 12345UL;
            var address = "ADDRESS1123456789012345678901234567890123456789012345";
            var actionType = WhitelistActionType.Add;
            var performedBy = TestUserAddress;
            var fromDate = DateTime.UtcNow.AddDays(-7);
            var toDate = DateTime.UtcNow;

            var response = new WhitelistAuditLogResponse
            {
                Success = true,
                Entries = new List<WhitelistAuditLogEntry>(),
                TotalCount = 0,
                Page = 1,
                PageSize = 50,
                TotalPages = 0
            };

            GetWhitelistAuditLogRequest? capturedRequest = null;
            _whitelistServiceMock
                .Setup(s => s.GetAuditLogAsync(It.IsAny<GetWhitelistAuditLogRequest>()))
                .Callback<GetWhitelistAuditLogRequest>(r => capturedRequest = r)
                .ReturnsAsync(response);

            // Act
            var result = await _controller.GetAuditLog(assetId, address, actionType, performedBy, fromDate, toDate, 2, 25);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            Assert.That(capturedRequest, Is.Not.Null);
            Assert.That(capturedRequest?.AssetId, Is.EqualTo(assetId));
            Assert.That(capturedRequest?.Address, Is.EqualTo(address));
            Assert.That(capturedRequest?.ActionType, Is.EqualTo(actionType));
            Assert.That(capturedRequest?.PerformedBy, Is.EqualTo(performedBy));
            Assert.That(capturedRequest?.FromDate, Is.EqualTo(fromDate));
            Assert.That(capturedRequest?.ToDate, Is.EqualTo(toDate));
            Assert.That(capturedRequest?.Page, Is.EqualTo(2));
            Assert.That(capturedRequest?.PageSize, Is.EqualTo(25));
        }

        [Test]
        public async Task GetAuditLog_ServiceFailure_ShouldReturnInternalServerError()
        {
            // Arrange
            var assetId = 12345UL;
            var response = new WhitelistAuditLogResponse
            {
                Success = false,
                ErrorMessage = "Database error"
            };

            _whitelistServiceMock
                .Setup(s => s.GetAuditLogAsync(It.IsAny<GetWhitelistAuditLogRequest>()))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.GetAuditLog(assetId);

            // Assert
            Assert.That(result, Is.InstanceOf<ObjectResult>());
            var objectResult = result as ObjectResult;
            Assert.That(objectResult?.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
        }

        [Test]
        public async Task GetAuditLog_Exception_ShouldReturnInternalServerError()
        {
            // Arrange
            var assetId = 12345UL;

            _whitelistServiceMock
                .Setup(s => s.GetAuditLogAsync(It.IsAny<GetWhitelistAuditLogRequest>()))
                .ThrowsAsync(new Exception("Unexpected error"));

            // Act
            var result = await _controller.GetAuditLog(assetId);

            // Assert
            Assert.That(result, Is.InstanceOf<ObjectResult>());
            var objectResult = result as ObjectResult;
            Assert.That(objectResult?.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
        }

        [Test]
        public async Task GetAuditLog_PageSizeExceedsMax_ShouldCapAt100()
        {
            // Arrange
            var assetId = 12345UL;
            var response = new WhitelistAuditLogResponse
            {
                Success = true,
                Entries = new List<WhitelistAuditLogEntry>(),
                TotalCount = 0,
                Page = 1,
                PageSize = 100,
                TotalPages = 0
            };

            GetWhitelistAuditLogRequest? capturedRequest = null;
            _whitelistServiceMock
                .Setup(s => s.GetAuditLogAsync(It.IsAny<GetWhitelistAuditLogRequest>()))
                .Callback<GetWhitelistAuditLogRequest>(r => capturedRequest = r)
                .ReturnsAsync(response);

            // Act
            var result = await _controller.GetAuditLog(assetId, pageSize: 200);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            Assert.That(capturedRequest?.PageSize, Is.EqualTo(100));
        }

        #endregion

        #region CSV Export Tests

        [Test]
        public async Task ExportWhitelistCsv_ValidRequest_ShouldReturnCsvFile()
        {
            // Arrange
            var assetId = 12345UL;
            var entries = new List<WhitelistEntry>
            {
                new WhitelistEntry
                {
                    Id = "entry1",
                    AssetId = assetId,
                    Address = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA",
                    Status = WhitelistStatus.Active,
                    CreatedBy = TestUserAddress,
                    CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    Reason = "Test reason",
                    KycVerified = true,
                    Network = "voimain-v1.0"
                }
            };

            var response = new WhitelistListResponse
            {
                Success = true,
                Entries = entries,
                TotalCount = 1
            };

            _whitelistServiceMock.Setup(s => s.ListEntriesAsync(It.IsAny<ListWhitelistRequest>()))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.ExportWhitelistCsv(assetId);

            // Assert
            Assert.That(result, Is.InstanceOf<FileContentResult>());
            var fileResult = result as FileContentResult;
            Assert.That(fileResult?.ContentType, Is.EqualTo("text/csv"));
            Assert.That(fileResult?.FileDownloadName, Does.StartWith($"whitelist-{assetId}-"));
            Assert.That(fileResult?.FileDownloadName, Does.EndWith(".csv"));

            // Verify CSV content
            var csvContent = System.Text.Encoding.UTF8.GetString(fileResult!.FileContents);
            Assert.That(csvContent, Does.Contain("Id,AssetId,Address,Status"));
            Assert.That(csvContent, Does.Contain("entry1"));
            Assert.That(csvContent, Does.Contain("VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA"));
        }

        [Test]
        public async Task ExportWhitelistCsv_EmptyWhitelist_ShouldReturnCsvWithHeaderOnly()
        {
            // Arrange
            var assetId = 12345UL;
            var response = new WhitelistListResponse
            {
                Success = true,
                Entries = new List<WhitelistEntry>(),
                TotalCount = 0
            };

            _whitelistServiceMock.Setup(s => s.ListEntriesAsync(It.IsAny<ListWhitelistRequest>()))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.ExportWhitelistCsv(assetId);

            // Assert
            Assert.That(result, Is.InstanceOf<FileContentResult>());
            var fileResult = result as FileContentResult;
            var csvContent = System.Text.Encoding.UTF8.GetString(fileResult!.FileContents);
            
            // Should have header row
            Assert.That(csvContent, Does.Contain("Id,AssetId,Address,Status"));
            
            // Should only have one line (header)
            var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.That(lines.Length, Is.EqualTo(1));
        }

        [Test]
        public async Task ExportWhitelistCsv_ServiceFailure_ShouldReturnInternalServerError()
        {
            // Arrange
            var assetId = 12345UL;
            var response = new WhitelistListResponse
            {
                Success = false,
                ErrorMessage = "Database error"
            };

            _whitelistServiceMock.Setup(s => s.ListEntriesAsync(It.IsAny<ListWhitelistRequest>()))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.ExportWhitelistCsv(assetId);

            // Assert
            Assert.That(result, Is.InstanceOf<ObjectResult>());
            var objectResult = result as ObjectResult;
            Assert.That(objectResult?.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
        }

        #endregion

        #region CSV Import Tests

        [Test]
        public async Task ImportWhitelistCsv_ValidCsv_ShouldReturnSuccess()
        {
            // Arrange
            var assetId = 12345UL;
            var csvContent = "Address,Status,Reason\n" +
                           "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA,Active,KYC Verified\n" +
                           "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ,Active,Accredited";

            var csvBytes = System.Text.Encoding.UTF8.GetBytes(csvContent);
            var file = new FormFile(new MemoryStream(csvBytes), 0, csvBytes.Length, "file", "test.csv")
            {
                Headers = new HeaderDictionary(),
                ContentType = "text/csv"
            };

            var bulkResponse = new BulkWhitelistResponse
            {
                Success = true,
                SuccessCount = 2,
                FailedCount = 0
            };

            _whitelistServiceMock.Setup(s => s.BulkAddEntriesAsync(It.IsAny<BulkAddWhitelistRequest>(), It.IsAny<string>()))
                .ReturnsAsync(bulkResponse);

            // Act
            var result = await _controller.ImportWhitelistCsv(assetId, file);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            var response = okResult?.Value as BulkWhitelistResponse;
            Assert.That(response?.Success, Is.True);
            Assert.That(response?.SuccessCount, Is.EqualTo(2));
            Assert.That(response?.FailedCount, Is.EqualTo(0));
        }

        [Test]
        public async Task ImportWhitelistCsv_NoFile_ShouldReturnBadRequest()
        {
            // Arrange
            var assetId = 12345UL;

            // Act
            var result = await _controller.ImportWhitelistCsv(assetId, null!);

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
            var badResult = result as BadRequestObjectResult;
            var response = badResult?.Value as BulkWhitelistResponse;
            Assert.That(response?.ErrorMessage, Does.Contain("No file uploaded"));
        }

        [Test]
        public async Task ImportWhitelistCsv_EmptyFile_ShouldReturnBadRequest()
        {
            // Arrange
            var assetId = 12345UL;
            var file = new FormFile(new MemoryStream(), 0, 0, "file", "test.csv")
            {
                Headers = new HeaderDictionary(),
                ContentType = "text/csv"
            };

            // Act
            var result = await _controller.ImportWhitelistCsv(assetId, file);

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task ImportWhitelistCsv_FileTooLarge_ShouldReturnBadRequest()
        {
            // Arrange
            var assetId = 12345UL;
            var largeBytes = new byte[2 * 1024 * 1024]; // 2 MB
            var file = new FormFile(new MemoryStream(largeBytes), 0, largeBytes.Length, "file", "test.csv")
            {
                Headers = new HeaderDictionary(),
                ContentType = "text/csv"
            };

            // Act
            var result = await _controller.ImportWhitelistCsv(assetId, file);

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
            var badResult = result as BadRequestObjectResult;
            var response = badResult?.Value as BulkWhitelistResponse;
            Assert.That(response?.ErrorMessage, Does.Contain("exceeds maximum"));
        }

        [Test]
        public async Task ImportWhitelistCsv_WrongFileExtension_ShouldReturnBadRequest()
        {
            // Arrange
            var assetId = 12345UL;
            var content = "some content";
            var bytes = System.Text.Encoding.UTF8.GetBytes(content);
            var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "test.txt")
            {
                Headers = new HeaderDictionary(),
                ContentType = "text/plain"
            };

            // Act
            var result = await _controller.ImportWhitelistCsv(assetId, file);

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
            var badResult = result as BadRequestObjectResult;
            var response = badResult?.Value as BulkWhitelistResponse;
            Assert.That(response?.ErrorMessage, Does.Contain(".csv extension"));
        }

        [Test]
        public async Task ImportWhitelistCsv_NoAddressColumn_ShouldReturnBadRequest()
        {
            // Arrange
            var assetId = 12345UL;
            var csvContent = "Name,Value\nTest,123";
            var csvBytes = System.Text.Encoding.UTF8.GetBytes(csvContent);
            var file = new FormFile(new MemoryStream(csvBytes), 0, csvBytes.Length, "file", "test.csv")
            {
                Headers = new HeaderDictionary(),
                ContentType = "text/csv"
            };

            // Act
            var result = await _controller.ImportWhitelistCsv(assetId, file);

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
            var badResult = result as BadRequestObjectResult;
            var response = badResult?.Value as BulkWhitelistResponse;
            Assert.That(response?.ErrorMessage, Does.Contain("'Address' column"));
        }

        [Test]
        public async Task ImportWhitelistCsv_NoValidAddresses_ShouldReturnBadRequest()
        {
            // Arrange
            var assetId = 12345UL;
            var csvContent = "Address\n\n\n"; // Empty addresses
            var csvBytes = System.Text.Encoding.UTF8.GetBytes(csvContent);
            var file = new FormFile(new MemoryStream(csvBytes), 0, csvBytes.Length, "file", "test.csv")
            {
                Headers = new HeaderDictionary(),
                ContentType = "text/csv"
            };

            // Act
            var result = await _controller.ImportWhitelistCsv(assetId, file);

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
            var badResult = result as BadRequestObjectResult;
            var response = badResult?.Value as BulkWhitelistResponse;
            Assert.That(response?.ErrorMessage, Does.Contain("No valid addresses"));
        }

        [Test]
        public async Task ImportWhitelistCsv_NoUserInContext_ShouldReturnUnauthorized()
        {
            // Arrange
            var assetId = 12345UL;
            var csvContent = "Address\nVCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";
            var csvBytes = System.Text.Encoding.UTF8.GetBytes(csvContent);
            var file = new FormFile(new MemoryStream(csvBytes), 0, csvBytes.Length, "file", "test.csv")
            {
                Headers = new HeaderDictionary(),
                ContentType = "text/csv"
            };

            // Set up controller with no user context
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
            };

            // Act
            var result = await _controller.ImportWhitelistCsv(assetId, file);

            // Assert
            Assert.That(result, Is.InstanceOf<UnauthorizedObjectResult>());
        }

        [Test]
        public async Task ImportWhitelistCsv_WithOptionalFields_ShouldParseCorrectly()
        {
            // Arrange
            var assetId = 12345UL;
            var csvContent = "Address,Status,Reason,KycVerified,Network\n" +
                           "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA,Active,KYC Verified,true,voimain-v1.0";

            var csvBytes = System.Text.Encoding.UTF8.GetBytes(csvContent);
            var file = new FormFile(new MemoryStream(csvBytes), 0, csvBytes.Length, "file", "test.csv")
            {
                Headers = new HeaderDictionary(),
                ContentType = "text/csv"
            };

            BulkAddWhitelistRequest? capturedRequest = null;
            _whitelistServiceMock.Setup(s => s.BulkAddEntriesAsync(It.IsAny<BulkAddWhitelistRequest>(), It.IsAny<string>()))
                .Callback<BulkAddWhitelistRequest, string>((r, u) => capturedRequest = r)
                .ReturnsAsync(new BulkWhitelistResponse { Success = true, SuccessCount = 1 });

            // Act
            var result = await _controller.ImportWhitelistCsv(assetId, file);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            Assert.That(capturedRequest, Is.Not.Null);
            Assert.That(capturedRequest!.AssetId, Is.EqualTo(assetId));
            Assert.That(capturedRequest.Addresses, Has.Count.EqualTo(1));
            Assert.That(capturedRequest.Status, Is.EqualTo(WhitelistStatus.Active));
            Assert.That(capturedRequest.Reason, Is.EqualTo("KYC Verified"));
            Assert.That(capturedRequest.KycVerified, Is.True);
            Assert.That(capturedRequest.Network, Is.EqualTo("voimain-v1.0"));
        }

        #endregion

        #region VerifyAllowlistStatus Tests

        [Test]
        public async Task VerifyAllowlistStatus_BothApproved_ShouldReturnAllowed()
        {
            // Arrange
            var request = new VerifyAllowlistStatusRequest
            {
                AssetId = 12345,
                SenderAddress = "SENDER1234567890123456789012345678901234567890123456789",
                RecipientAddress = "RECIPIENT1234567890123456789012345678901234567890123456",
                Network = "voimain-v1.0"
            };

            var response = new VerifyAllowlistStatusResponse
            {
                Success = true,
                AssetId = request.AssetId,
                SenderStatus = new AllowlistParticipantStatus
                {
                    Address = request.SenderAddress,
                    Status = AllowlistStatus.Approved,
                    IsWhitelisted = true,
                    KycVerified = true
                },
                RecipientStatus = new AllowlistParticipantStatus
                {
                    Address = request.RecipientAddress,
                    Status = AllowlistStatus.Approved,
                    IsWhitelisted = true,
                    KycVerified = true
                },
                TransferStatus = AllowlistTransferStatus.Allowed,
                MicaDisclosure = new MicaComplianceDisclosure
                {
                    RequiresMicaCompliance = true,
                    Network = "voimain-v1.0"
                },
                AuditMetadata = new AllowlistAuditMetadata
                {
                    PerformedBy = TestUserAddress
                },
                CacheDurationSeconds = 60
            };

            _whitelistServiceMock.Setup(s => s.VerifyAllowlistStatusAsync(
                It.IsAny<VerifyAllowlistStatusRequest>(), TestUserAddress))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.VerifyAllowlistStatus(request);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            var verifyResponse = okResult?.Value as VerifyAllowlistStatusResponse;
            Assert.That(verifyResponse?.Success, Is.True);
            Assert.That(verifyResponse?.TransferStatus, Is.EqualTo(AllowlistTransferStatus.Allowed));
            Assert.That(verifyResponse?.SenderStatus?.Status, Is.EqualTo(AllowlistStatus.Approved));
            Assert.That(verifyResponse?.RecipientStatus?.Status, Is.EqualTo(AllowlistStatus.Approved));
        }

        [Test]
        public async Task VerifyAllowlistStatus_SenderExpired_ShouldReturnBlockedSender()
        {
            // Arrange
            var request = new VerifyAllowlistStatusRequest
            {
                AssetId = 12345,
                SenderAddress = "SENDER1234567890123456789012345678901234567890123456789",
                RecipientAddress = "RECIPIENT1234567890123456789012345678901234567890123456",
                Network = "aramidmain-v1.0"
            };

            var response = new VerifyAllowlistStatusResponse
            {
                Success = true,
                AssetId = request.AssetId,
                SenderStatus = new AllowlistParticipantStatus
                {
                    Address = request.SenderAddress,
                    Status = AllowlistStatus.Expired,
                    IsWhitelisted = true,
                    ExpirationDate = DateTime.UtcNow.AddDays(-1),
                    StatusNotes = "Whitelist entry has expired"
                },
                RecipientStatus = new AllowlistParticipantStatus
                {
                    Address = request.RecipientAddress,
                    Status = AllowlistStatus.Approved,
                    IsWhitelisted = true
                },
                TransferStatus = AllowlistTransferStatus.BlockedSender
            };

            _whitelistServiceMock.Setup(s => s.VerifyAllowlistStatusAsync(
                It.IsAny<VerifyAllowlistStatusRequest>(), TestUserAddress))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.VerifyAllowlistStatus(request);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            var verifyResponse = okResult?.Value as VerifyAllowlistStatusResponse;
            Assert.That(verifyResponse?.TransferStatus, Is.EqualTo(AllowlistTransferStatus.BlockedSender));
            Assert.That(verifyResponse?.SenderStatus?.Status, Is.EqualTo(AllowlistStatus.Expired));
        }

        [Test]
        public async Task VerifyAllowlistStatus_RecipientDenied_ShouldReturnBlockedRecipient()
        {
            // Arrange
            var request = new VerifyAllowlistStatusRequest
            {
                AssetId = 12345,
                SenderAddress = "SENDER1234567890123456789012345678901234567890123456789",
                RecipientAddress = "RECIPIENT1234567890123456789012345678901234567890123456"
            };

            var response = new VerifyAllowlistStatusResponse
            {
                Success = true,
                AssetId = request.AssetId,
                SenderStatus = new AllowlistParticipantStatus
                {
                    Address = request.SenderAddress,
                    Status = AllowlistStatus.Approved,
                    IsWhitelisted = true
                },
                RecipientStatus = new AllowlistParticipantStatus
                {
                    Address = request.RecipientAddress,
                    Status = AllowlistStatus.Denied,
                    IsWhitelisted = false,
                    StatusNotes = "Address is not whitelisted"
                },
                TransferStatus = AllowlistTransferStatus.BlockedRecipient
            };

            _whitelistServiceMock.Setup(s => s.VerifyAllowlistStatusAsync(
                It.IsAny<VerifyAllowlistStatusRequest>(), TestUserAddress))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.VerifyAllowlistStatus(request);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            var verifyResponse = okResult?.Value as VerifyAllowlistStatusResponse;
            Assert.That(verifyResponse?.TransferStatus, Is.EqualTo(AllowlistTransferStatus.BlockedRecipient));
            Assert.That(verifyResponse?.RecipientStatus?.Status, Is.EqualTo(AllowlistStatus.Denied));
        }

        [Test]
        public async Task VerifyAllowlistStatus_BothDenied_ShouldReturnBlockedBoth()
        {
            // Arrange
            var request = new VerifyAllowlistStatusRequest
            {
                AssetId = 12345,
                SenderAddress = "SENDER1234567890123456789012345678901234567890123456789",
                RecipientAddress = "RECIPIENT1234567890123456789012345678901234567890123456"
            };

            var response = new VerifyAllowlistStatusResponse
            {
                Success = true,
                AssetId = request.AssetId,
                SenderStatus = new AllowlistParticipantStatus
                {
                    Address = request.SenderAddress,
                    Status = AllowlistStatus.Denied,
                    IsWhitelisted = false
                },
                RecipientStatus = new AllowlistParticipantStatus
                {
                    Address = request.RecipientAddress,
                    Status = AllowlistStatus.Denied,
                    IsWhitelisted = false
                },
                TransferStatus = AllowlistTransferStatus.BlockedBoth
            };

            _whitelistServiceMock.Setup(s => s.VerifyAllowlistStatusAsync(
                It.IsAny<VerifyAllowlistStatusRequest>(), TestUserAddress))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.VerifyAllowlistStatus(request);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            var verifyResponse = okResult?.Value as VerifyAllowlistStatusResponse;
            Assert.That(verifyResponse?.TransferStatus, Is.EqualTo(AllowlistTransferStatus.BlockedBoth));
        }

        [Test]
        public async Task VerifyAllowlistStatus_SenderPending_ShouldReturnBlockedSender()
        {
            // Arrange
            var request = new VerifyAllowlistStatusRequest
            {
                AssetId = 12345,
                SenderAddress = "SENDER1234567890123456789012345678901234567890123456789",
                RecipientAddress = "RECIPIENT1234567890123456789012345678901234567890123456"
            };

            var response = new VerifyAllowlistStatusResponse
            {
                Success = true,
                AssetId = request.AssetId,
                SenderStatus = new AllowlistParticipantStatus
                {
                    Address = request.SenderAddress,
                    Status = AllowlistStatus.Pending,
                    IsWhitelisted = true,
                    StatusNotes = "Whitelist entry is pending or inactive"
                },
                RecipientStatus = new AllowlistParticipantStatus
                {
                    Address = request.RecipientAddress,
                    Status = AllowlistStatus.Approved,
                    IsWhitelisted = true
                },
                TransferStatus = AllowlistTransferStatus.BlockedSender
            };

            _whitelistServiceMock.Setup(s => s.VerifyAllowlistStatusAsync(
                It.IsAny<VerifyAllowlistStatusRequest>(), TestUserAddress))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.VerifyAllowlistStatus(request);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            var verifyResponse = okResult?.Value as VerifyAllowlistStatusResponse;
            Assert.That(verifyResponse?.TransferStatus, Is.EqualTo(AllowlistTransferStatus.BlockedSender));
            Assert.That(verifyResponse?.SenderStatus?.Status, Is.EqualTo(AllowlistStatus.Pending));
        }

        [Test]
        public async Task VerifyAllowlistStatus_MicaNetwork_ShouldIncludeMicaDisclosure()
        {
            // Arrange
            var request = new VerifyAllowlistStatusRequest
            {
                AssetId = 12345,
                SenderAddress = "SENDER1234567890123456789012345678901234567890123456789",
                RecipientAddress = "RECIPIENT1234567890123456789012345678901234567890123456",
                Network = "voimain-v1.0"
            };

            var response = new VerifyAllowlistStatusResponse
            {
                Success = true,
                AssetId = request.AssetId,
                SenderStatus = new AllowlistParticipantStatus
                {
                    Address = request.SenderAddress,
                    Status = AllowlistStatus.Approved,
                    IsWhitelisted = true
                },
                RecipientStatus = new AllowlistParticipantStatus
                {
                    Address = request.RecipientAddress,
                    Status = AllowlistStatus.Approved,
                    IsWhitelisted = true
                },
                TransferStatus = AllowlistTransferStatus.Allowed,
                MicaDisclosure = new MicaComplianceDisclosure
                {
                    RequiresMicaCompliance = true,
                    Network = "voimain-v1.0",
                    ApplicableRegulations = new List<string>
                    {
                        "MiCA Article 41 - Safeguarding of crypto-assets and client funds",
                        "MiCA Article 76 - Obligations of issuers of asset-referenced tokens",
                        "MiCA Article 88 - Whitelist and KYC requirements for RWA tokens"
                    },
                    ComplianceNotes = "Network voimain-v1.0 requires MICA compliance. Ensure all participants complete KYC verification and maintain active whitelist status."
                }
            };

            _whitelistServiceMock.Setup(s => s.VerifyAllowlistStatusAsync(
                It.IsAny<VerifyAllowlistStatusRequest>(), TestUserAddress))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.VerifyAllowlistStatus(request);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            var verifyResponse = okResult?.Value as VerifyAllowlistStatusResponse;
            Assert.That(verifyResponse?.MicaDisclosure, Is.Not.Null);
            Assert.That(verifyResponse?.MicaDisclosure?.RequiresMicaCompliance, Is.True);
            Assert.That(verifyResponse?.MicaDisclosure?.ApplicableRegulations, Has.Count.EqualTo(3));
        }

        [Test]
        public async Task VerifyAllowlistStatus_WithAuditMetadata_ShouldIncludeAuditInfo()
        {
            // Arrange
            var request = new VerifyAllowlistStatusRequest
            {
                AssetId = 12345,
                SenderAddress = "SENDER1234567890123456789012345678901234567890123456789",
                RecipientAddress = "RECIPIENT1234567890123456789012345678901234567890123456"
            };

            var verificationId = Guid.NewGuid().ToString();
            var response = new VerifyAllowlistStatusResponse
            {
                Success = true,
                AssetId = request.AssetId,
                SenderStatus = new AllowlistParticipantStatus
                {
                    Address = request.SenderAddress,
                    Status = AllowlistStatus.Approved,
                    IsWhitelisted = true
                },
                RecipientStatus = new AllowlistParticipantStatus
                {
                    Address = request.RecipientAddress,
                    Status = AllowlistStatus.Approved,
                    IsWhitelisted = true
                },
                TransferStatus = AllowlistTransferStatus.Allowed,
                AuditMetadata = new AllowlistAuditMetadata
                {
                    VerificationId = verificationId,
                    PerformedBy = TestUserAddress,
                    VerifiedAt = DateTime.UtcNow,
                    Source = "API"
                }
            };

            _whitelistServiceMock.Setup(s => s.VerifyAllowlistStatusAsync(
                It.IsAny<VerifyAllowlistStatusRequest>(), TestUserAddress))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.VerifyAllowlistStatus(request);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            var verifyResponse = okResult?.Value as VerifyAllowlistStatusResponse;
            Assert.That(verifyResponse?.AuditMetadata, Is.Not.Null);
            Assert.That(verifyResponse?.AuditMetadata?.PerformedBy, Is.EqualTo(TestUserAddress));
            Assert.That(verifyResponse?.AuditMetadata?.Source, Is.EqualTo("API"));
        }

        [Test]
        public async Task VerifyAllowlistStatus_ShouldSetCacheControlHeader()
        {
            // Arrange
            var request = new VerifyAllowlistStatusRequest
            {
                AssetId = 12345,
                SenderAddress = "SENDER1234567890123456789012345678901234567890123456789",
                RecipientAddress = "RECIPIENT1234567890123456789012345678901234567890123456"
            };

            var response = new VerifyAllowlistStatusResponse
            {
                Success = true,
                AssetId = request.AssetId,
                SenderStatus = new AllowlistParticipantStatus
                {
                    Address = request.SenderAddress,
                    Status = AllowlistStatus.Approved,
                    IsWhitelisted = true
                },
                RecipientStatus = new AllowlistParticipantStatus
                {
                    Address = request.RecipientAddress,
                    Status = AllowlistStatus.Approved,
                    IsWhitelisted = true
                },
                TransferStatus = AllowlistTransferStatus.Allowed,
                CacheDurationSeconds = 60
            };

            _whitelistServiceMock.Setup(s => s.VerifyAllowlistStatusAsync(
                It.IsAny<VerifyAllowlistStatusRequest>(), TestUserAddress))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.VerifyAllowlistStatus(request);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            Assert.That(_controller.Response.Headers.ContainsKey("Cache-Control"), Is.True);
            Assert.That(_controller.Response.Headers["Cache-Control"].ToString(), Is.EqualTo("public, max-age=60"));
        }

        [Test]
        public async Task VerifyAllowlistStatus_InvalidModelState_ShouldReturnBadRequest()
        {
            // Arrange
            var request = new VerifyAllowlistStatusRequest
            {
                AssetId = 12345,
                SenderAddress = "INVALID",
                RecipientAddress = "INVALID"
            };
            _controller.ModelState.AddModelError("SenderAddress", "Invalid address");

            // Act
            var result = await _controller.VerifyAllowlistStatus(request);

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task VerifyAllowlistStatus_NoUserContext_ShouldReturnUnauthorized()
        {
            // Arrange
            var request = new VerifyAllowlistStatusRequest
            {
                AssetId = 12345,
                SenderAddress = "SENDER1234567890123456789012345678901234567890123456789",
                RecipientAddress = "RECIPIENT1234567890123456789012345678901234567890123456"
            };

            // Set up controller with no user context
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
            };

            // Act
            var result = await _controller.VerifyAllowlistStatus(request);

            // Assert
            Assert.That(result, Is.InstanceOf<UnauthorizedObjectResult>());
        }

        [Test]
        public async Task VerifyAllowlistStatus_ServiceFailure_ShouldReturnBadRequest()
        {
            // Arrange
            var request = new VerifyAllowlistStatusRequest
            {
                AssetId = 12345,
                SenderAddress = "SENDER1234567890123456789012345678901234567890123456789",
                RecipientAddress = "RECIPIENT1234567890123456789012345678901234567890123456"
            };

            var response = new VerifyAllowlistStatusResponse
            {
                Success = false,
                ErrorMessage = "Invalid sender address format",
                AssetId = request.AssetId
            };

            _whitelistServiceMock.Setup(s => s.VerifyAllowlistStatusAsync(
                It.IsAny<VerifyAllowlistStatusRequest>(), TestUserAddress))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.VerifyAllowlistStatus(request);

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task VerifyAllowlistStatus_ServiceException_ShouldReturnInternalServerError()
        {
            // Arrange
            var request = new VerifyAllowlistStatusRequest
            {
                AssetId = 12345,
                SenderAddress = "SENDER1234567890123456789012345678901234567890123456789",
                RecipientAddress = "RECIPIENT1234567890123456789012345678901234567890123456"
            };

            _whitelistServiceMock.Setup(s => s.VerifyAllowlistStatusAsync(
                It.IsAny<VerifyAllowlistStatusRequest>(), TestUserAddress))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.VerifyAllowlistStatus(request);

            // Assert
            Assert.That(result, Is.InstanceOf<ObjectResult>());
            var objectResult = result as ObjectResult;
            Assert.That(objectResult?.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
        }

        #endregion
    }
}

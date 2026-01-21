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
    }
}

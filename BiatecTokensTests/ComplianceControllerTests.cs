using BiatecTokensApi.Controllers;
using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;

namespace BiatecTokensTests
{
    [TestFixture]
    public class ComplianceControllerTests
    {
        private Mock<IComplianceService> _serviceMock;
        private Mock<ILogger<ComplianceController>> _loggerMock;
        private ComplianceController _controller;
        private const string TestUserAddress = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";

        [SetUp]
        public void Setup()
        {
            _serviceMock = new Mock<IComplianceService>();
            _loggerMock = new Mock<ILogger<ComplianceController>>();
            _controller = new ComplianceController(_serviceMock.Object, _loggerMock.Object);

            // Mock authenticated user context
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, TestUserAddress)
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var claimsPrincipal = new ClaimsPrincipal(identity);

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = claimsPrincipal
                }
            };
        }

        #region GetComplianceMetadata Tests

        [Test]
        public async Task GetComplianceMetadata_ExistingMetadata_ShouldReturnOk()
        {
            // Arrange
            var assetId = 12345ul;
            var metadata = new ComplianceMetadata
            {
                AssetId = assetId,
                ComplianceStatus = ComplianceStatus.Compliant
            };

            _serviceMock.Setup(s => s.GetMetadataAsync(assetId))
                .ReturnsAsync(new ComplianceMetadataResponse
                {
                    Success = true,
                    Metadata = metadata
                });

            // Act
            var result = await _controller.GetComplianceMetadata(assetId);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            Assert.That(okResult!.Value, Is.InstanceOf<ComplianceMetadataResponse>());
            var response = okResult.Value as ComplianceMetadataResponse;
            Assert.That(response!.Success, Is.True);
            Assert.That(response.Metadata!.AssetId, Is.EqualTo(assetId));
        }

        [Test]
        public async Task GetComplianceMetadata_NonExistingMetadata_ShouldReturnNotFound()
        {
            // Arrange
            var assetId = 99999ul;
            _serviceMock.Setup(s => s.GetMetadataAsync(assetId))
                .ReturnsAsync(new ComplianceMetadataResponse
                {
                    Success = false,
                    ErrorMessage = "Compliance metadata not found"
                });

            // Act
            var result = await _controller.GetComplianceMetadata(assetId);

            // Assert
            Assert.That(result, Is.InstanceOf<NotFoundObjectResult>());
        }

        #endregion

        #region UpsertComplianceMetadata Tests

        [Test]
        public async Task UpsertComplianceMetadata_ValidRequest_ShouldReturnOk()
        {
            // Arrange
            var request = new UpsertComplianceMetadataRequest
            {
                AssetId = 12345,
                KycProvider = "Sumsub",
                ComplianceStatus = ComplianceStatus.Compliant
            };

            var metadata = new ComplianceMetadata
            {
                AssetId = request.AssetId,
                KycProvider = request.KycProvider,
                ComplianceStatus = request.ComplianceStatus,
                CreatedBy = TestUserAddress
            };

            _serviceMock.Setup(s => s.UpsertMetadataAsync(request, TestUserAddress))
                .ReturnsAsync(new ComplianceMetadataResponse
                {
                    Success = true,
                    Metadata = metadata
                });

            // Act
            var result = await _controller.UpsertComplianceMetadata(request);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            var response = okResult!.Value as ComplianceMetadataResponse;
            Assert.That(response!.Success, Is.True);
            Assert.That(response.Metadata!.CreatedBy, Is.EqualTo(TestUserAddress));
        }

        [Test]
        public async Task UpsertComplianceMetadata_NetworkValidationFailure_ShouldReturnBadRequest()
        {
            // Arrange
            var request = new UpsertComplianceMetadataRequest
            {
                AssetId = 12345,
                Network = "voimain-v1.0",
                RequiresAccreditedInvestors = true,
                VerificationStatus = VerificationStatus.Pending
            };

            _serviceMock.Setup(s => s.UpsertMetadataAsync(request, TestUserAddress))
                .ReturnsAsync(new ComplianceMetadataResponse
                {
                    Success = false,
                    ErrorMessage = "VOI network requires KYC verification"
                });

            // Act
            var result = await _controller.UpsertComplianceMetadata(request);

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task UpsertComplianceMetadata_InvalidModelState_ShouldReturnBadRequest()
        {
            // Arrange
            var request = new UpsertComplianceMetadataRequest();
            _controller.ModelState.AddModelError("AssetId", "AssetId is required");

            // Act
            var result = await _controller.UpsertComplianceMetadata(request);

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task UpsertComplianceMetadata_NoUserContext_ShouldReturnUnauthorized()
        {
            // Arrange - Remove user context
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };

            var request = new UpsertComplianceMetadataRequest
            {
                AssetId = 12345
            };

            // Act
            var result = await _controller.UpsertComplianceMetadata(request);

            // Assert
            Assert.That(result, Is.InstanceOf<UnauthorizedObjectResult>());
        }

        #endregion

        #region DeleteComplianceMetadata Tests

        [Test]
        public async Task DeleteComplianceMetadata_ExistingMetadata_ShouldReturnOk()
        {
            // Arrange
            var assetId = 12345ul;
            _serviceMock.Setup(s => s.DeleteMetadataAsync(assetId))
                .ReturnsAsync(new ComplianceMetadataResponse
                {
                    Success = true
                });

            // Act
            var result = await _controller.DeleteComplianceMetadata(assetId);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
        }

        [Test]
        public async Task DeleteComplianceMetadata_NonExistingMetadata_ShouldReturnNotFound()
        {
            // Arrange
            var assetId = 99999ul;
            _serviceMock.Setup(s => s.DeleteMetadataAsync(assetId))
                .ReturnsAsync(new ComplianceMetadataResponse
                {
                    Success = false,
                    ErrorMessage = "Compliance metadata not found"
                });

            // Act
            var result = await _controller.DeleteComplianceMetadata(assetId);

            // Assert
            Assert.That(result, Is.InstanceOf<NotFoundObjectResult>());
        }

        #endregion

        #region ListComplianceMetadata Tests

        [Test]
        public async Task ListComplianceMetadata_NoFilters_ShouldReturnOk()
        {
            // Arrange
            var metadataList = new List<ComplianceMetadata>
            {
                new ComplianceMetadata { AssetId = 1 },
                new ComplianceMetadata { AssetId = 2 },
                new ComplianceMetadata { AssetId = 3 }
            };

            _serviceMock.Setup(s => s.ListMetadataAsync(It.IsAny<ListComplianceMetadataRequest>()))
                .ReturnsAsync(new ComplianceMetadataListResponse
                {
                    Success = true,
                    Metadata = metadataList,
                    TotalCount = 3,
                    Page = 1,
                    PageSize = 20,
                    TotalPages = 1
                });

            // Act
            var result = await _controller.ListComplianceMetadata();

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            var response = okResult!.Value as ComplianceMetadataListResponse;
            Assert.That(response!.Success, Is.True);
            Assert.That(response.Metadata.Count, Is.EqualTo(3));
        }

        [Test]
        public async Task ListComplianceMetadata_WithFilters_ShouldPassFiltersToService()
        {
            // Arrange
            var complianceStatus = ComplianceStatus.Compliant;
            var verificationStatus = VerificationStatus.Verified;
            var network = "voimain-v1.0";

            _serviceMock.Setup(s => s.ListMetadataAsync(It.Is<ListComplianceMetadataRequest>(
                r => r.ComplianceStatus == complianceStatus &&
                     r.VerificationStatus == verificationStatus &&
                     r.Network == network)))
                .ReturnsAsync(new ComplianceMetadataListResponse
                {
                    Success = true,
                    Metadata = new List<ComplianceMetadata>()
                });

            // Act
            var result = await _controller.ListComplianceMetadata(
                complianceStatus, 
                verificationStatus, 
                network);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            _serviceMock.Verify(s => s.ListMetadataAsync(It.Is<ListComplianceMetadataRequest>(
                r => r.ComplianceStatus == complianceStatus &&
                     r.VerificationStatus == verificationStatus &&
                     r.Network == network)), Times.Once);
        }

        [Test]
        public async Task ListComplianceMetadata_WithPagination_ShouldCapPageSize()
        {
            // Arrange
            _serviceMock.Setup(s => s.ListMetadataAsync(It.IsAny<ListComplianceMetadataRequest>()))
                .ReturnsAsync(new ComplianceMetadataListResponse
                {
                    Success = true,
                    Metadata = new List<ComplianceMetadata>()
                });

            // Act
            var result = await _controller.ListComplianceMetadata(
                pageSize: 150); // Exceeds max of 100

            // Assert
            _serviceMock.Verify(s => s.ListMetadataAsync(It.Is<ListComplianceMetadataRequest>(
                r => r.PageSize == 100)), Times.Once); // Should be capped at 100
        }

        #endregion

        #region Exception Handling Tests

        [Test]
        public async Task GetComplianceMetadata_ServiceThrowsException_ShouldReturn500()
        {
            // Arrange
            var assetId = 12345ul;
            _serviceMock.Setup(s => s.GetMetadataAsync(assetId))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.GetComplianceMetadata(assetId);

            // Assert
            Assert.That(result, Is.InstanceOf<ObjectResult>());
            var objectResult = result as ObjectResult;
            Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
        }

        [Test]
        public async Task UpsertComplianceMetadata_ServiceThrowsException_ShouldReturn500()
        {
            // Arrange
            var request = new UpsertComplianceMetadataRequest
            {
                AssetId = 12345
            };
            _serviceMock.Setup(s => s.UpsertMetadataAsync(request, TestUserAddress))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.UpsertComplianceMetadata(request);

            // Assert
            Assert.That(result, Is.InstanceOf<ObjectResult>());
            var objectResult = result as ObjectResult;
            Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
        }

        #endregion
    }
}

using BiatecTokensApi.Controllers;
using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace BiatecTokensTests
{
    [TestFixture]
    public class CapabilityMatrixControllerTests
    {
        private Mock<ICapabilityMatrixService> _serviceMock;
        private Mock<ILogger<CapabilityMatrixController>> _loggerMock;
        private CapabilityMatrixController _controller;

        [SetUp]
        public void Setup()
        {
            _serviceMock = new Mock<ICapabilityMatrixService>();
            _loggerMock = new Mock<ILogger<CapabilityMatrixController>>();
            _controller = new CapabilityMatrixController(_serviceMock.Object, _loggerMock.Object);
        }

        #region GetCapabilityMatrix Tests

        [Test]
        public async Task GetCapabilityMatrix_NoFilters_ShouldReturnOk()
        {
            // Arrange
            var response = new CapabilityMatrixResponse
            {
                Success = true,
                Data = new CapabilityMatrix
                {
                    Version = "2026-02-05",
                    GeneratedAt = DateTime.UtcNow,
                    Jurisdictions = new List<JurisdictionCapability>
                    {
                        new JurisdictionCapability
                        {
                            Code = "CH",
                            Name = "Switzerland",
                            WalletTypes = new List<WalletTypeCapability>()
                        }
                    }
                }
            };

            _serviceMock.Setup(s => s.GetCapabilityMatrixAsync(It.IsAny<GetCapabilityMatrixRequest>()))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.GetCapabilityMatrix(null, null, null, null);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            Assert.That(okResult!.Value, Is.InstanceOf<CapabilityMatrixResponse>());
            var matrixResponse = okResult.Value as CapabilityMatrixResponse;
            Assert.That(matrixResponse!.Success, Is.True);
        }

        [Test]
        public async Task GetCapabilityMatrix_WithJurisdictionFilter_ShouldReturnOk()
        {
            // Arrange
            var response = new CapabilityMatrixResponse
            {
                Success = true,
                Data = new CapabilityMatrix
                {
                    Version = "2026-02-05",
                    GeneratedAt = DateTime.UtcNow,
                    Jurisdictions = new List<JurisdictionCapability>
                    {
                        new JurisdictionCapability
                        {
                            Code = "CH",
                            Name = "Switzerland",
                            WalletTypes = new List<WalletTypeCapability>()
                        }
                    }
                }
            };

            _serviceMock.Setup(s => s.GetCapabilityMatrixAsync(It.Is<GetCapabilityMatrixRequest>(r => r.Jurisdiction == "CH")))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.GetCapabilityMatrix("CH", null, null, null);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            var matrixResponse = okResult!.Value as CapabilityMatrixResponse;
            Assert.That(matrixResponse!.Data!.Jurisdictions[0].Code, Is.EqualTo("CH"));
        }

        [Test]
        public async Task GetCapabilityMatrix_NoMatchingCapabilities_ShouldReturnNotFound()
        {
            // Arrange
            var response = new CapabilityMatrixResponse
            {
                Success = false,
                ErrorMessage = "No matching capabilities found",
                ErrorDetails = new CapabilityErrorDetails
                {
                    Error = "no_matching_capabilities",
                    Jurisdiction = "ZZ"
                }
            };

            _serviceMock.Setup(s => s.GetCapabilityMatrixAsync(It.IsAny<GetCapabilityMatrixRequest>()))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.GetCapabilityMatrix("ZZ", null, null, null);

            // Assert
            Assert.That(result, Is.InstanceOf<NotFoundObjectResult>());
            var notFoundResult = result as NotFoundObjectResult;
            var matrixResponse = notFoundResult!.Value as CapabilityMatrixResponse;
            Assert.That(matrixResponse!.Success, Is.False);
            Assert.That(matrixResponse.ErrorDetails!.Error, Is.EqualTo("no_matching_capabilities"));
        }

        [Test]
        public async Task GetCapabilityMatrix_InternalError_ShouldReturnInternalServerError()
        {
            // Arrange
            var response = new CapabilityMatrixResponse
            {
                Success = false,
                ErrorMessage = "Internal error"
            };

            _serviceMock.Setup(s => s.GetCapabilityMatrixAsync(It.IsAny<GetCapabilityMatrixRequest>()))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.GetCapabilityMatrix(null, null, null, null);

            // Assert
            Assert.That(result, Is.InstanceOf<ObjectResult>());
            var objectResult = result as ObjectResult;
            Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
        }

        [Test]
        public async Task GetCapabilityMatrix_ServiceThrowsException_ShouldReturnInternalServerError()
        {
            // Arrange
            _serviceMock.Setup(s => s.GetCapabilityMatrixAsync(It.IsAny<GetCapabilityMatrixRequest>()))
                .ThrowsAsync(new Exception("Test exception"));

            // Act
            var result = await _controller.GetCapabilityMatrix(null, null, null, null);

            // Assert
            Assert.That(result, Is.InstanceOf<ObjectResult>());
            var objectResult = result as ObjectResult;
            Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
        }

        [Test]
        public async Task GetCapabilityMatrix_WithAllFilters_ShouldPassAllFiltersToService()
        {
            // Arrange
            var response = new CapabilityMatrixResponse
            {
                Success = true,
                Data = new CapabilityMatrix
                {
                    Version = "2026-02-05",
                    GeneratedAt = DateTime.UtcNow,
                    Jurisdictions = new List<JurisdictionCapability>()
                }
            };

            GetCapabilityMatrixRequest? capturedRequest = null;
            _serviceMock.Setup(s => s.GetCapabilityMatrixAsync(It.IsAny<GetCapabilityMatrixRequest>()))
                .Callback<GetCapabilityMatrixRequest>(r => capturedRequest = r)
                .ReturnsAsync(response);

            // Act
            await _controller.GetCapabilityMatrix("CH", "custodial", "ARC-19", "2");

            // Assert
            Assert.That(capturedRequest, Is.Not.Null);
            Assert.That(capturedRequest!.Jurisdiction, Is.EqualTo("CH"));
            Assert.That(capturedRequest.WalletType, Is.EqualTo("custodial"));
            Assert.That(capturedRequest.TokenStandard, Is.EqualTo("ARC-19"));
            Assert.That(capturedRequest.KycTier, Is.EqualTo("2"));
        }

        #endregion

        #region CheckCapability Tests

        [Test]
        public async Task CheckCapability_ValidRequest_AllowedAction_ShouldReturnOk()
        {
            // Arrange
            var request = new CapabilityCheckRequest
            {
                Jurisdiction = "CH",
                WalletType = "custodial",
                TokenStandard = "ARC-19",
                KycTier = "2",
                Action = "mint"
            };

            var response = new CapabilityCheckResponse
            {
                Allowed = true,
                RequiredChecks = new List<string> { "sanctions", "accreditation" }
            };

            _serviceMock.Setup(s => s.CheckCapabilityAsync(It.IsAny<CapabilityCheckRequest>()))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.CheckCapability(request);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            var checkResponse = okResult!.Value as CapabilityCheckResponse;
            Assert.That(checkResponse!.Allowed, Is.True);
            Assert.That(checkResponse.RequiredChecks, Has.Count.EqualTo(2));
        }

        [Test]
        public async Task CheckCapability_ValidRequest_DisallowedAction_ShouldReturnForbidden()
        {
            // Arrange
            var request = new CapabilityCheckRequest
            {
                Jurisdiction = "CH",
                WalletType = "custodial",
                TokenStandard = "ARC-19",
                KycTier = "2",
                Action = "freeze"
            };

            var response = new CapabilityCheckResponse
            {
                Allowed = false,
                Reason = "Action not allowed",
                ErrorDetails = new CapabilityErrorDetails
                {
                    Error = "capability_not_allowed",
                    RuleId = "action_not_allowed"
                }
            };

            _serviceMock.Setup(s => s.CheckCapabilityAsync(It.IsAny<CapabilityCheckRequest>()))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.CheckCapability(request);

            // Assert
            Assert.That(result, Is.InstanceOf<ObjectResult>());
            var objectResult = result as ObjectResult;
            Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
            var checkResponse = objectResult.Value as CapabilityCheckResponse;
            Assert.That(checkResponse!.Allowed, Is.False);
        }

        [Test]
        public async Task CheckCapability_MissingJurisdiction_ShouldReturnBadRequest()
        {
            // Arrange
            var request = new CapabilityCheckRequest
            {
                Jurisdiction = "", // Missing
                WalletType = "custodial",
                TokenStandard = "ARC-19",
                KycTier = "2",
                Action = "mint"
            };

            // Act
            var result = await _controller.CheckCapability(request);

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
            var badRequestResult = result as BadRequestObjectResult;
            var checkResponse = badRequestResult!.Value as CapabilityCheckResponse;
            Assert.That(checkResponse!.Allowed, Is.False);
            Assert.That(checkResponse.Reason, Does.Contain("required"));
        }

        [Test]
        public async Task CheckCapability_MissingWalletType_ShouldReturnBadRequest()
        {
            // Arrange
            var request = new CapabilityCheckRequest
            {
                Jurisdiction = "CH",
                WalletType = "", // Missing
                TokenStandard = "ARC-19",
                KycTier = "2",
                Action = "mint"
            };

            // Act
            var result = await _controller.CheckCapability(request);

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task CheckCapability_MissingTokenStandard_ShouldReturnBadRequest()
        {
            // Arrange
            var request = new CapabilityCheckRequest
            {
                Jurisdiction = "CH",
                WalletType = "custodial",
                TokenStandard = "", // Missing
                KycTier = "2",
                Action = "mint"
            };

            // Act
            var result = await _controller.CheckCapability(request);

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task CheckCapability_MissingKycTier_ShouldReturnBadRequest()
        {
            // Arrange
            var request = new CapabilityCheckRequest
            {
                Jurisdiction = "CH",
                WalletType = "custodial",
                TokenStandard = "ARC-19",
                KycTier = "", // Missing
                Action = "mint"
            };

            // Act
            var result = await _controller.CheckCapability(request);

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task CheckCapability_MissingAction_ShouldReturnBadRequest()
        {
            // Arrange
            var request = new CapabilityCheckRequest
            {
                Jurisdiction = "CH",
                WalletType = "custodial",
                TokenStandard = "ARC-19",
                KycTier = "2",
                Action = "" // Missing
            };

            // Act
            var result = await _controller.CheckCapability(request);

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task CheckCapability_ServiceThrowsException_ShouldReturnInternalServerError()
        {
            // Arrange
            var request = new CapabilityCheckRequest
            {
                Jurisdiction = "CH",
                WalletType = "custodial",
                TokenStandard = "ARC-19",
                KycTier = "2",
                Action = "mint"
            };

            _serviceMock.Setup(s => s.CheckCapabilityAsync(It.IsAny<CapabilityCheckRequest>()))
                .ThrowsAsync(new Exception("Test exception"));

            // Act
            var result = await _controller.CheckCapability(request);

            // Assert
            Assert.That(result, Is.InstanceOf<ObjectResult>());
            var objectResult = result as ObjectResult;
            Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
        }

        #endregion

        #region GetVersion Tests

        [Test]
        public void GetVersion_ShouldReturnOkWithVersion()
        {
            // Arrange
            _serviceMock.Setup(s => s.GetVersion()).Returns("2026-02-05");

            // Act
            var result = _controller.GetVersion();

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            Assert.That(okResult!.Value, Is.Not.Null);
            
            // Check if the value has a version property
            var versionObject = okResult.Value;
            var versionProperty = versionObject!.GetType().GetProperty("version");
            Assert.That(versionProperty, Is.Not.Null);
            var version = versionProperty!.GetValue(versionObject) as string;
            Assert.That(version, Is.EqualTo("2026-02-05"));
        }

        [Test]
        public void GetVersion_ServiceThrowsException_ShouldReturnInternalServerError()
        {
            // Arrange
            _serviceMock.Setup(s => s.GetVersion()).Throws(new Exception("Test exception"));

            // Act
            var result = _controller.GetVersion();

            // Assert
            Assert.That(result, Is.InstanceOf<ObjectResult>());
            var objectResult = result as ObjectResult;
            Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
        }

        #endregion
    }
}

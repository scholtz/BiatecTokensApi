using BiatecTokensApi.Controllers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace BiatecTokensTests
{
    [TestFixture]
    public class TokenControllerTests
    {
        private Mock<ITokenService> _tokenServiceMock;
        private Mock<ILogger<TokenController>> _loggerMock;
        private TokenController _controller;
        private TokenDeploymentRequest _validDeploymentRequest;

        [SetUp]
        public void Setup()
        {
            _tokenServiceMock = new Mock<ITokenService>();
            _loggerMock = new Mock<ILogger<TokenController>>();
            _controller = new TokenController(_tokenServiceMock.Object, _loggerMock.Object);

            // Set up a valid token deployment request for testing
            _validDeploymentRequest = new TokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "TEST",
                InitialSupply = 1000000,
                Decimals = 18,
                DeployerPrivateKey = "0xabc123def456abc123def456abc123def456abc123def456abc123def456abcd"
            };

            // Set up controller for testing validation
            _controller.ModelState.Clear();
        }

        [Test]
        public async Task DeployToken_WithValidRequest_ReturnsOkResult()
        {
            // Arrange
            var expectedResponse = new TokenDeploymentResponse
            {
                TransactionHash = "0xabc123def456",
                ContractAddress = "0x1234567890abcdef",
                Success = true
            };

            _tokenServiceMock.Setup(ts => ts.DeployTokenAsync(It.IsAny<TokenDeploymentRequest>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.DeployToken(_validDeploymentRequest);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);
            Assert.That(okResult!.Value, Is.EqualTo(expectedResponse));
        }

        [Test]
        public async Task DeployToken_WithInvalidModel_ReturnsBadRequest()
        {
            // Arrange
            _controller.ModelState.AddModelError("Name", "Name is required");

            // Act
            var result = await _controller.DeployToken(_validDeploymentRequest);

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task DeployToken_WhenServiceFails_ReturnsInternalServerError()
        {
            // Arrange
            var failedResponse = new TokenDeploymentResponse
            {
                Success = false,
                ErrorMessage = "Unable to deploy token"
            };

            _tokenServiceMock.Setup(ts => ts.DeployTokenAsync(It.IsAny<TokenDeploymentRequest>()))
                .ReturnsAsync(failedResponse);

            // Act
            var result = await _controller.DeployToken(_validDeploymentRequest);

            // Assert
            Assert.That(result, Is.InstanceOf<ObjectResult>());
            var statusCodeResult = result as ObjectResult;
            Assert.That(statusCodeResult, Is.Not.Null);
            Assert.That(statusCodeResult!.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
            Assert.That(statusCodeResult.Value, Is.EqualTo(failedResponse));
        }

        [Test]
        public async Task DeployToken_WhenServiceThrowsException_ReturnsInternalServerError()
        {
            // Arrange
            var exceptionMessage = "Unexpected error";
            _tokenServiceMock.Setup(ts => ts.DeployTokenAsync(It.IsAny<TokenDeploymentRequest>()))
                .ThrowsAsync(new Exception(exceptionMessage));

            // Act
            var result = await _controller.DeployToken(_validDeploymentRequest);

            // Assert
            Assert.That(result, Is.InstanceOf<ObjectResult>());
            var statusCodeResult = result as ObjectResult;
            Assert.That(statusCodeResult, Is.Not.Null);
            Assert.That(statusCodeResult!.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));

            var responseValue = JsonSerializer.Serialize(statusCodeResult.Value);
            Assert.That(responseValue, Does.Contain(exceptionMessage));
        }

        [Test]
        public void TokenDeploymentRequest_ValidatesRequiredFields()
        {
            // Arrange
            var context = new ValidationContext(_validDeploymentRequest);
            var results = new List<ValidationResult>();

            // Act - Validate with all properties
            var isValid = Validator.TryValidateObject(_validDeploymentRequest, context, results, true);
            
            // Assert - Valid when all required fields are present
            Assert.That(isValid, Is.True);

            // Create a new instance of TokenDeploymentRequest for testing validation errors
            var emptyNameRequest = new TokenDeploymentRequest
            {
                Name = "",  // Empty name
                Symbol = "TEST",
                InitialSupply = 1000,
                DeployerPrivateKey = "0xkey"
            };

            // Create validation context for this request
            context = new ValidationContext(emptyNameRequest);
            results = new List<ValidationResult>();

            // Act - Validate with empty name
            isValid = Validator.TryValidateObject(emptyNameRequest, context, results, true);

            // Assert - Should be invalid due to empty name
            Assert.That(isValid, Is.False);
            Assert.That(results.Any(r => r.MemberNames.Contains("Name")), Is.True);

            // Create another instance with empty symbol
            var emptySymbolRequest = new TokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "",  // Empty symbol
                InitialSupply = 1000,
                DeployerPrivateKey = "0xkey"
            };

            // Create validation context for this request
            context = new ValidationContext(emptySymbolRequest);
            results = new List<ValidationResult>();

            // Act - Validate with empty symbol
            isValid = Validator.TryValidateObject(emptySymbolRequest, context, results, true);

            // Assert - Should be invalid due to empty symbol
            Assert.That(isValid, Is.False);
            Assert.That(results.Any(r => r.MemberNames.Contains("Symbol")), Is.True);
        }

        [Test]
        public async Task DeployToken_LogsInformation_OnSuccess()
        {
            // Arrange
            var successResponse = new TokenDeploymentResponse
            {
                Success = true,
                ContractAddress = "0x1234567890abcdef",
            };

            _tokenServiceMock.Setup(ts => ts.DeployTokenAsync(It.IsAny<TokenDeploymentRequest>()))
                .ReturnsAsync(successResponse);

            // Act
            await _controller.DeployToken(_validDeploymentRequest);

            // Assert - Verify logging occurred
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains(successResponse.ContractAddress!)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Test]
        public async Task DeployToken_LogsError_OnFailure()
        {
            // Arrange
            var errorMessage = "Deployment failed";
            var failedResponse = new TokenDeploymentResponse
            {
                Success = false,
                ErrorMessage = errorMessage,
            };

            _tokenServiceMock.Setup(ts => ts.DeployTokenAsync(It.IsAny<TokenDeploymentRequest>()))
                .ReturnsAsync(failedResponse);

            // Act
            await _controller.DeployToken(_validDeploymentRequest);

            // Assert - Verify error was logged
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains(errorMessage)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
    }
}
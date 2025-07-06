using BiatecTokensApi.Controllers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging;
using Moq;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace BiatecTokensTests
{
    [TestFixture]
    public class TokenControllerTests
    {
        private Mock<IERC20TokenService> _tokenServiceMock;
        private Mock<IARC3FungibleTokenService> _arc3TokenServiceMock;
        private Mock<ILogger<TokenController>> _loggerMock;
        private TokenController _controller;
        private TokenDeploymentRequest _validDeploymentRequest;
        private ASATokenDeploymentRequest _validASARequest;

        [SetUp]
        public void Setup()
        {
            _tokenServiceMock = new Mock<IERC20TokenService>();
            _arc3TokenServiceMock = new Mock<IARC3FungibleTokenService>();
            _loggerMock = new Mock<ILogger<TokenController>>();
            _controller = new TokenController(_tokenServiceMock.Object, _arc3TokenServiceMock.Object, _loggerMock.Object);

            // Set up a valid token deployment request for testing
            _validDeploymentRequest = new TokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "TEST",
                InitialSupply = 1000000,
                Decimals = 18,
                InitialSupplyReceiver = null, // Will default to deployer
                DeployerPrivateKey = "0xabc123def456abc123def456abc123def456abc123def456abc123def456abcd"
            };

            // Set up a valid ASA token deployment request for testing
            _validASARequest = new ASATokenDeploymentRequest
            {
                Name = "Test ASA Token",
                UnitName = "TASA",
                TotalSupply = 1000000,
                Decimals = 6,
                DefaultFrozen = false,
                CreatorMnemonic = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon art",
                Network = "testnet"
            };

            // Set up controller for testing validation
            _controller.ModelState.Clear();
        }

        #region CreateASAToken Tests

        [Test]
        public async Task CreateASAToken_WithValidRequest_ReturnsOkResult()
        {
            // Arrange
            var expectedResponse = new ARC3TokenDeploymentResponse
            {
                Success = true,
                AssetId = 12345,
                TransactionId = "TESTTXID123",
                CreatorAddress = "TESTADDRESS123",
                ConfirmedRound = 1000,
                TokenInfo = new ARC3TokenInfo
                {
                    Name = _validASARequest.Name,
                    UnitName = _validASARequest.UnitName,
                    TotalSupply = _validASARequest.TotalSupply,
                    Decimals = _validASARequest.Decimals,
                    DefaultFrozen = _validASARequest.DefaultFrozen
                }
            };

            _arc3TokenServiceMock
                .Setup(x => x.CreateTokenAsync(It.IsAny<ARC3FungibleTokenDeploymentRequest>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.CreateASAToken(_validASARequest);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            Assert.That(okResult?.Value, Is.EqualTo(expectedResponse));

            // Verify the service was called with correct parameters
            _arc3TokenServiceMock.Verify(x => x.CreateTokenAsync(It.Is<ARC3FungibleTokenDeploymentRequest>(req =>
                req.Name == _validASARequest.Name &&
                req.UnitName == _validASARequest.UnitName &&
                req.TotalSupply == _validASARequest.TotalSupply &&
                req.Decimals == _validASARequest.Decimals &&
                req.DefaultFrozen == _validASARequest.DefaultFrozen &&
                req.CreatorMnemonic == _validASARequest.CreatorMnemonic &&
                req.Network == _validASARequest.Network &&
                req.Metadata == null // ASA should have no metadata
            )), Times.Once);
        }

        [Test]
        public async Task CreateASAToken_EnsuresNoMetadata_CallsServiceWithNullMetadata()
        {
            // Arrange
            var expectedResponse = new ARC3TokenDeploymentResponse { Success = true };
            _arc3TokenServiceMock
                .Setup(x => x.CreateTokenAsync(It.IsAny<ARC3FungibleTokenDeploymentRequest>()))
                .ReturnsAsync(expectedResponse);

            // Act
            await _controller.CreateASAToken(_validASARequest);

            // Assert - Verify that Metadata is null in the ARC3 request
            _arc3TokenServiceMock.Verify(x => x.CreateTokenAsync(It.Is<ARC3FungibleTokenDeploymentRequest>(req =>
                req.Metadata == null
            )), Times.Once);
        }

        [Test]
        public async Task CreateASAToken_WithInvalidModelState_ReturnsBadRequest()
        {
            // Arrange
            _controller.ModelState.AddModelError("Name", "Name is required");

            // Act
            var result = await _controller.CreateASAToken(_validASARequest);

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
            _arc3TokenServiceMock.Verify(x => x.CreateTokenAsync(It.IsAny<ARC3FungibleTokenDeploymentRequest>()), Times.Never);
        }

        [Test]
        public async Task CreateASAToken_WithMissingRequiredFields_ReturnsBadRequest()
        {
            // Arrange
            var invalidRequest = new ASATokenDeploymentRequest
            {
                Name = "", // Invalid: empty name
                UnitName = "TEST",
                TotalSupply = 1000,
                CreatorMnemonic = "test mnemonic",
                Network = "testnet"
            };

            var validationContext = new ValidationContext(invalidRequest);
            var validationResults = new List<ValidationResult>();
            var isValid = Validator.TryValidateObject(invalidRequest, validationContext, validationResults, true);

            // Add validation errors to ModelState
            foreach (var validationResult in validationResults)
            {
                _controller.ModelState.AddModelError(validationResult.MemberNames.First(), validationResult.ErrorMessage ?? "");
            }

            // Act
            var result = await _controller.CreateASAToken(invalidRequest);

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
            _arc3TokenServiceMock.Verify(x => x.CreateTokenAsync(It.IsAny<ARC3FungibleTokenDeploymentRequest>()), Times.Never);
        }

        [Test]
        public async Task CreateASAToken_WithServiceFailure_ReturnsInternalServerError()
        {
            // Arrange
            var failureResponse = new ARC3TokenDeploymentResponse
            {
                Success = false,
                ErrorMessage = "Network connection failed"
            };

            _arc3TokenServiceMock
                .Setup(x => x.CreateTokenAsync(It.IsAny<ARC3FungibleTokenDeploymentRequest>()))
                .ReturnsAsync(failureResponse);

            // Act
            var result = await _controller.CreateASAToken(_validASARequest);

            // Assert
            Assert.That(result, Is.InstanceOf<ObjectResult>());
            var objectResult = result as ObjectResult;
            Assert.That(objectResult?.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
            Assert.That(objectResult?.Value, Is.EqualTo(failureResponse));
        }

        [Test]
        public async Task CreateASAToken_WithServiceException_ReturnsInternalServerError()
        {
            // Arrange
            var expectedException = new Exception("Service unavailable");
            _arc3TokenServiceMock
                .Setup(x => x.CreateTokenAsync(It.IsAny<ARC3FungibleTokenDeploymentRequest>()))
                .ThrowsAsync(expectedException);

            // Act
            var result = await _controller.CreateASAToken(_validASARequest);

            // Assert
            Assert.That(result, Is.InstanceOf<ObjectResult>());
            var objectResult = result as ObjectResult;
            Assert.That(objectResult?.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
            
            // Check the error message in the anonymous object
            var errorResponse = objectResult?.Value;
            Assert.That(errorResponse, Is.Not.Null);
            
            // Use reflection to check the error property
            var errorProperty = errorResponse?.GetType().GetProperty("error");
            Assert.That(errorProperty, Is.Not.Null);
            Assert.That(errorProperty?.GetValue(errorResponse)?.ToString(), Is.EqualTo("Service unavailable"));
        }

        [Test]
        public async Task CreateASAToken_WithOptionalAddresses_PassesAllAddressesToService()
        {
            // Arrange
            var requestWithAddresses = new ASATokenDeploymentRequest
            {
                Name = "Test ASA with Addresses",
                UnitName = "TASA",
                TotalSupply = 1000000,
                Decimals = 6,
                DefaultFrozen = true,
                ManagerAddress = "MANAGER_ADDRESS_123",
                ReserveAddress = "RESERVE_ADDRESS_123",
                FreezeAddress = "FREEZE_ADDRESS_123",
                ClawbackAddress = "CLAWBACK_ADDRESS_123",
                CreatorMnemonic = "test mnemonic",
                Network = "testnet"
            };

            var expectedResponse = new ARC3TokenDeploymentResponse { Success = true };
            _arc3TokenServiceMock
                .Setup(x => x.CreateTokenAsync(It.IsAny<ARC3FungibleTokenDeploymentRequest>()))
                .ReturnsAsync(expectedResponse);

            // Act
            await _controller.CreateASAToken(requestWithAddresses);

            // Assert
            _arc3TokenServiceMock.Verify(x => x.CreateTokenAsync(It.Is<ARC3FungibleTokenDeploymentRequest>(req =>
                req.ManagerAddress == requestWithAddresses.ManagerAddress &&
                req.ReserveAddress == requestWithAddresses.ReserveAddress &&
                req.FreezeAddress == requestWithAddresses.FreezeAddress &&
                req.ClawbackAddress == requestWithAddresses.ClawbackAddress &&
                req.DefaultFrozen == requestWithAddresses.DefaultFrozen
            )), Times.Once);
        }

        [Test]
        public async Task CreateASAToken_WithDifferentDecimals_PassesCorrectDecimalsToService()
        {
            // Arrange
            var requestWithCustomDecimals = new ASATokenDeploymentRequest
            {
                Name = "Test ASA Decimals",
                UnitName = "TASA",
                TotalSupply = 1000000,
                Decimals = 18, // Different from default
                CreatorMnemonic = "test mnemonic",
                Network = "testnet"
            };

            var expectedResponse = new ARC3TokenDeploymentResponse { Success = true };
            _arc3TokenServiceMock
                .Setup(x => x.CreateTokenAsync(It.IsAny<ARC3FungibleTokenDeploymentRequest>()))
                .ReturnsAsync(expectedResponse);

            // Act
            await _controller.CreateASAToken(requestWithCustomDecimals);

            // Assert
            _arc3TokenServiceMock.Verify(x => x.CreateTokenAsync(It.Is<ARC3FungibleTokenDeploymentRequest>(req =>
                req.Decimals == 18
            )), Times.Once);
        }

        [Test]
        public async Task CreateASAToken_WithDifferentNetworks_PassesCorrectNetworkToService()
        {
            // Arrange
            var requestWithMainnet = new ASATokenDeploymentRequest
            {
                Name = "Test ASA Mainnet",
                UnitName = "TASA",
                TotalSupply = 1000000,
                Decimals = 6,
                CreatorMnemonic = "test mnemonic",
                Network = "mainnet"
            };

            var expectedResponse = new ARC3TokenDeploymentResponse { Success = true };
            _arc3TokenServiceMock
                .Setup(x => x.CreateTokenAsync(It.IsAny<ARC3FungibleTokenDeploymentRequest>()))
                .ReturnsAsync(expectedResponse);

            // Act
            await _controller.CreateASAToken(requestWithMainnet);

            // Assert
            _arc3TokenServiceMock.Verify(x => x.CreateTokenAsync(It.Is<ARC3FungibleTokenDeploymentRequest>(req =>
                req.Network == "mainnet"
            )), Times.Once);
        }

        [Test]
        public async Task CreateASAToken_LogsSuccessfulCreation_WhenTokenCreatedSuccessfully()
        {
            // Arrange
            var expectedResponse = new ARC3TokenDeploymentResponse
            {
                Success = true,
                AssetId = 12345,
                TransactionId = "TESTTXID123"
            };

            _arc3TokenServiceMock
                .Setup(x => x.CreateTokenAsync(It.IsAny<ARC3FungibleTokenDeploymentRequest>()))
                .ReturnsAsync(expectedResponse);

            // Act
            await _controller.CreateASAToken(_validASARequest);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("ASA token created successfully")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Test]
        public async Task CreateASAToken_LogsFailure_WhenTokenCreationFails()
        {
            // Arrange
            var failureResponse = new ARC3TokenDeploymentResponse
            {
                Success = false,
                ErrorMessage = "Network connection failed"
            };

            _arc3TokenServiceMock
                .Setup(x => x.CreateTokenAsync(It.IsAny<ARC3FungibleTokenDeploymentRequest>()))
                .ReturnsAsync(failureResponse);

            // Act
            await _controller.CreateASAToken(_validASARequest);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("ASA token creation failed")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Test]
        public async Task CreateASAToken_WithMaximumValues_HandlesLargeNumbers()
        {
            // Arrange
            var requestWithMaxValues = new ASATokenDeploymentRequest
            {
                Name = new string('A', 32), // Maximum length name
                UnitName = new string('B', 8), // Maximum length unit name
                TotalSupply = ulong.MaxValue, // Maximum supply
                Decimals = 19, // Maximum decimals
                CreatorMnemonic = "test mnemonic",
                Network = "testnet"
            };

            var expectedResponse = new ARC3TokenDeploymentResponse { Success = true };
            _arc3TokenServiceMock
                .Setup(x => x.CreateTokenAsync(It.IsAny<ARC3FungibleTokenDeploymentRequest>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.CreateASAToken(requestWithMaxValues);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            _arc3TokenServiceMock.Verify(x => x.CreateTokenAsync(It.Is<ARC3FungibleTokenDeploymentRequest>(req =>
                req.Name == requestWithMaxValues.Name &&
                req.UnitName == requestWithMaxValues.UnitName &&
                req.TotalSupply == ulong.MaxValue &&
                req.Decimals == 19
            )), Times.Once);
        }

        [Test]
        public async Task CreateASAToken_WithMinimumValues_HandlesBasicConfiguration()
        {
            // Arrange
            var requestWithMinValues = new ASATokenDeploymentRequest
            {
                Name = "A", // Minimum length name
                UnitName = "B", // Minimum length unit name
                TotalSupply = 1, // Minimum supply
                Decimals = 0, // Minimum decimals
                CreatorMnemonic = "test mnemonic",
                Network = "testnet"
            };

            var expectedResponse = new ARC3TokenDeploymentResponse { Success = true };
            _arc3TokenServiceMock
                .Setup(x => x.CreateTokenAsync(It.IsAny<ARC3FungibleTokenDeploymentRequest>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.CreateASAToken(requestWithMinValues);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            _arc3TokenServiceMock.Verify(x => x.CreateTokenAsync(It.Is<ARC3FungibleTokenDeploymentRequest>(req =>
                req.Name == "A" &&
                req.UnitName == "B" &&
                req.TotalSupply == 1 &&
                req.Decimals == 0
            )), Times.Once);
        }

        [Test]
        public async Task CreateASAToken_WithBetanetNetwork_PassesCorrectNetwork()
        {
            // Arrange
            var requestWithBetanet = new ASATokenDeploymentRequest
            {
                Name = "Beta ASA",
                UnitName = "BETA",
                TotalSupply = 1000,
                Decimals = 6,
                CreatorMnemonic = "test mnemonic",
                Network = "betanet"
            };

            var expectedResponse = new ARC3TokenDeploymentResponse { Success = true };
            _arc3TokenServiceMock
                .Setup(x => x.CreateTokenAsync(It.IsAny<ARC3FungibleTokenDeploymentRequest>()))
                .ReturnsAsync(expectedResponse);

            // Act
            await _controller.CreateASAToken(requestWithBetanet);

            // Assert
            _arc3TokenServiceMock.Verify(x => x.CreateTokenAsync(It.Is<ARC3FungibleTokenDeploymentRequest>(req =>
                req.Network == "betanet"
            )), Times.Once);
        }

        #endregion

        #region Helper Methods

        private static ASATokenDeploymentRequest CreateValidASARequest(string name = "Test ASA", string unitName = "TASA")
        {
            return new ASATokenDeploymentRequest
            {
                Name = name,
                UnitName = unitName,
                TotalSupply = 1000000,
                Decimals = 6,
                CreatorMnemonic = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon art",
                Network = "testnet"
            };
        }

        private static void AddValidationErrorsToModelState(object model, ModelStateDictionary modelState)
        {
            var validationContext = new ValidationContext(model);
            var validationResults = new List<ValidationResult>();
            Validator.TryValidateObject(model, validationContext, validationResults, true);

            foreach (var validationResult in validationResults)
            {
                var memberName = validationResult.MemberNames.FirstOrDefault() ?? "Unknown";
                modelState.AddModelError(memberName, validationResult.ErrorMessage ?? "Validation error");
            }
        }

        #endregion
    }
}
using BiatecTokensApi.Controllers;
using BiatecTokensApi.Models.TokenStandards;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Integration tests for TokenStandardsController
    /// </summary>
    [TestFixture]
    public class TokenStandardsControllerTests
    {
        private Mock<ITokenStandardRegistry> _registryMock;
        private Mock<ITokenStandardValidator> _validatorMock;
        private Mock<ILogger<TokenStandardsController>> _loggerMock;
        private TokenStandardsController _controller;

        [SetUp]
        public void SetUp()
        {
            _registryMock = new Mock<ITokenStandardRegistry>();
            _validatorMock = new Mock<ITokenStandardValidator>();
            _loggerMock = new Mock<ILogger<TokenStandardsController>>();
            _controller = new TokenStandardsController(
                _registryMock.Object,
                _validatorMock.Object,
                _loggerMock.Object);
        }

        [Test]
        public async Task GetStandards_ReturnsOk_WithStandardsList()
        {
            // Arrange
            var standards = new List<TokenStandardProfile>
            {
                new TokenStandardProfile 
                { 
                    Id = "baseline-1.0",
                    Standard = TokenStandard.Baseline, 
                    Name = "Baseline" 
                },
                new TokenStandardProfile 
                { 
                    Id = "arc3-1.0",
                    Standard = TokenStandard.ARC3, 
                    Name = "ARC-3" 
                }
            };
            _registryMock.Setup(r => r.GetAllStandardsAsync(It.IsAny<bool>()))
                .ReturnsAsync(standards);

            // Act
            var result = await _controller.GetStandards(null);

            // Assert
            Assert.That(result, Is.TypeOf<OkObjectResult>());
            var okResult = (OkObjectResult)result;
            Assert.That(okResult.Value, Is.TypeOf<GetTokenStandardsResponse>());
            var response = (GetTokenStandardsResponse)okResult.Value;
            Assert.That(response.TotalCount, Is.EqualTo(2));
            Assert.That(response.Standards.Count, Is.EqualTo(2));
        }

        [Test]
        public async Task GetStandards_FiltersActiveOnly()
        {
            // Arrange
            var activeStandards = new List<TokenStandardProfile>
            {
                new TokenStandardProfile 
                { 
                    Standard = TokenStandard.Baseline, 
                    IsActive = true 
                }
            };
            _registryMock.Setup(r => r.GetAllStandardsAsync(true))
                .ReturnsAsync(activeStandards);

            var request = new GetTokenStandardsRequest { ActiveOnly = true };

            // Act
            var result = await _controller.GetStandards(request);

            // Assert
            Assert.That(result, Is.TypeOf<OkObjectResult>());
            var okResult = (OkObjectResult)result;
            Assert.That(okResult.Value, Is.TypeOf<GetTokenStandardsResponse>());
            var response = (GetTokenStandardsResponse)okResult.Value;
            foreach (var standard in response.Standards)
            {
                Assert.That(standard.IsActive, Is.True);
            }
        }

        [Test]
        public async Task GetStandards_FiltersSpecificStandard()
        {
            // Arrange
            var allStandards = new List<TokenStandardProfile>
            {
                new TokenStandardProfile 
                { 
                    Standard = TokenStandard.ARC3, 
                    Name = "ARC-3" 
                },
                new TokenStandardProfile 
                { 
                    Standard = TokenStandard.Baseline, 
                    Name = "Baseline" 
                }
            };
            _registryMock.Setup(r => r.GetAllStandardsAsync(It.IsAny<bool>()))
                .ReturnsAsync(allStandards);

            var request = new GetTokenStandardsRequest { Standard = TokenStandard.ARC3 };

            // Act
            var result = await _controller.GetStandards(request);

            // Assert
            Assert.That(result, Is.TypeOf<OkObjectResult>());
            var okResult = (OkObjectResult)result;
            Assert.That(okResult.Value, Is.TypeOf<GetTokenStandardsResponse>());
            var response = (GetTokenStandardsResponse)okResult.Value;
            Assert.That(response.Standards, Has.Count.EqualTo(1));
            Assert.That(response.Standards[0].Standard, Is.EqualTo(TokenStandard.ARC3));
        }

        [Test]
        public async Task GetStandard_ReturnsOk_ForValidStandard()
        {
            // Arrange
            var profile = new TokenStandardProfile
            {
                Id = "arc3-1.0",
                Standard = TokenStandard.ARC3,
                Name = "ARC-3",
                Version = "1.0.0"
            };
            _registryMock.Setup(r => r.GetStandardProfileAsync(TokenStandard.ARC3))
                .ReturnsAsync(profile);

            // Act
            var result = await _controller.GetStandard(TokenStandard.ARC3);

            // Assert
            Assert.That(result, Is.TypeOf<OkObjectResult>());
            var okResult = (OkObjectResult)result;
            Assert.That(okResult.Value, Is.TypeOf<TokenStandardProfile>());
            var returnedProfile = (TokenStandardProfile)okResult.Value;
            Assert.That(returnedProfile.Standard, Is.EqualTo(TokenStandard.ARC3));
        }

        [Test]
        public async Task GetStandard_ReturnsNotFound_ForUnsupportedStandard()
        {
            // Arrange
            _registryMock.Setup(r => r.GetStandardProfileAsync(It.IsAny<TokenStandard>()))
                .ReturnsAsync((TokenStandardProfile?)null);

            // Act
            var result = await _controller.GetStandard(TokenStandard.ARC3);

            // Assert
            Assert.That(result, Is.TypeOf<NotFoundObjectResult>());
        }

        [Test]
        public async Task ValidateMetadata_ReturnsOk_ForValidMetadata()
        {
            // Arrange
            _registryMock.Setup(r => r.IsStandardSupportedAsync(TokenStandard.ARC3))
                .ReturnsAsync(true);

            var validationResult = new TokenValidationResult
            {
                IsValid = true,
                Standard = TokenStandard.ARC3,
                StandardVersion = "1.0.0",
                Message = "Validation passed successfully"
            };
            _validatorMock.Setup(v => v.ValidateAsync(
                    It.IsAny<TokenStandard>(),
                    It.IsAny<object>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<int?>()))
                .ReturnsAsync(validationResult);

            var request = new ValidateTokenMetadataRequest
            {
                Standard = TokenStandard.ARC3,
                Name = "Test Token",
                Metadata = new { description = "A test token" }
            };

            // Act
            var result = await _controller.ValidateMetadata(request);

            // Assert
            Assert.That(result, Is.TypeOf<OkObjectResult>());
            var okResult = (OkObjectResult)result;
            Assert.That(okResult.Value, Is.TypeOf<ValidateTokenMetadataResponse>());
            var response = (ValidateTokenMetadataResponse)okResult.Value;
            Assert.That(response.IsValid, Is.True);
            Assert.That(response.CorrelationId, Is.Not.Null);
        }

        [Test]
        public async Task ValidateMetadata_ReturnsBadRequest_ForUnsupportedStandard()
        {
            // Arrange
            _registryMock.Setup(r => r.IsStandardSupportedAsync(It.IsAny<TokenStandard>()))
                .ReturnsAsync(false);

            var request = new ValidateTokenMetadataRequest
            {
                Standard = TokenStandard.ARC3,
                Metadata = new { }
            };

            // Act
            var result = await _controller.ValidateMetadata(request);

            // Assert
            Assert.That(result, Is.TypeOf<BadRequestObjectResult>());
            var badRequestResult = (BadRequestObjectResult)result;
            Assert.That(badRequestResult.Value, Is.TypeOf<ValidateTokenMetadataResponse>());
            var response = (ValidateTokenMetadataResponse)badRequestResult.Value;
            Assert.That(response.IsValid, Is.False);
        }

        [Test]
        public async Task ValidateMetadata_ReturnsErrors_ForInvalidMetadata()
        {
            // Arrange
            _registryMock.Setup(r => r.IsStandardSupportedAsync(TokenStandard.ARC3))
                .ReturnsAsync(true);

            var validationResult = new TokenValidationResult
            {
                IsValid = false,
                Standard = TokenStandard.ARC3,
                StandardVersion = "1.0.0",
                Errors = new List<ValidationError>
                {
                    new ValidationError
                    {
                        Code = "REQUIRED_FIELD_MISSING",
                        Field = "name",
                        Message = "Name is required"
                    }
                }
            };
            _validatorMock.Setup(v => v.ValidateAsync(
                    It.IsAny<TokenStandard>(),
                    It.IsAny<object>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<int?>()))
                .ReturnsAsync(validationResult);

            var request = new ValidateTokenMetadataRequest
            {
                Standard = TokenStandard.ARC3,
                Metadata = new { }
            };

            // Act
            var result = await _controller.ValidateMetadata(request);

            // Assert
            Assert.That(result, Is.TypeOf<OkObjectResult>());
            var okResult = (OkObjectResult)result;
            Assert.That(okResult.Value, Is.TypeOf<ValidateTokenMetadataResponse>());
            var response = (ValidateTokenMetadataResponse)okResult.Value;
            Assert.That(response.IsValid, Is.False);
            Assert.That(response.ValidationResult, Is.Not.Null);
            Assert.That(response.ValidationResult.Errors, Is.Not.Empty);
        }

        [Test]
        public async Task ValidateMetadata_IncludesCorrelationId()
        {
            // Arrange
            _registryMock.Setup(r => r.IsStandardSupportedAsync(It.IsAny<TokenStandard>()))
                .ReturnsAsync(true);

            var validationResult = new TokenValidationResult
            {
                IsValid = true,
                Standard = TokenStandard.Baseline
            };
            _validatorMock.Setup(v => v.ValidateAsync(
                    It.IsAny<TokenStandard>(),
                    It.IsAny<object>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<int?>()))
                .ReturnsAsync(validationResult);

            var request = new ValidateTokenMetadataRequest
            {
                Standard = TokenStandard.Baseline,
                Name = "Test"
            };

            // Act
            var result = await _controller.ValidateMetadata(request);

            // Assert
            Assert.That(result, Is.TypeOf<OkObjectResult>());
            var okResult = (OkObjectResult)result;
            Assert.That(okResult.Value, Is.TypeOf<ValidateTokenMetadataResponse>());
            var response = (ValidateTokenMetadataResponse)okResult.Value;
            Assert.That(response.CorrelationId, Is.Not.Null);
            Assert.That(response.CorrelationId, Is.Not.Empty);
        }

        [Test]
        public async Task ValidateMetadata_PassesContextFieldsToValidator()
        {
            // Arrange
            _registryMock.Setup(r => r.IsStandardSupportedAsync(It.IsAny<TokenStandard>()))
                .ReturnsAsync(true);

            var validationResult = new TokenValidationResult { IsValid = true };
            _validatorMock.Setup(v => v.ValidateAsync(
                    TokenStandard.ERC20,
                    It.IsAny<object>(),
                    "My Token",
                    "MTK",
                    6))
                .ReturnsAsync(validationResult);

            var request = new ValidateTokenMetadataRequest
            {
                Standard = TokenStandard.ERC20,
                Name = "My Token",
                Symbol = "MTK",
                Decimals = 6
            };

            // Act
            await _controller.ValidateMetadata(request);

            // Assert
            _validatorMock.Verify(v => v.ValidateAsync(
                TokenStandard.ERC20,
                It.IsAny<object>(),
                "My Token",
                "MTK",
                6), Times.Once);
        }

        [Test]
        public async Task ValidateMetadata_HandlesValidationWarnings()
        {
            // Arrange
            _registryMock.Setup(r => r.IsStandardSupportedAsync(TokenStandard.ARC3))
                .ReturnsAsync(true);

            var validationResult = new TokenValidationResult
            {
                IsValid = true,
                Standard = TokenStandard.ARC3,
                Warnings = new List<ValidationError>
                {
                    new ValidationError
                    {
                        Code = "WARNING_CODE",
                        Field = "image_mimetype",
                        Message = "MIME type should start with image/",
                        Severity = ValidationSeverity.Warning
                    }
                }
            };
            _validatorMock.Setup(v => v.ValidateAsync(
                    It.IsAny<TokenStandard>(),
                    It.IsAny<object>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<int?>()))
                .ReturnsAsync(validationResult);

            var request = new ValidateTokenMetadataRequest
            {
                Standard = TokenStandard.ARC3,
                Metadata = new { name = "Test" }
            };

            // Act
            var result = await _controller.ValidateMetadata(request);

            // Assert
            Assert.That(result, Is.TypeOf<OkObjectResult>());
            var okResult = (OkObjectResult)result;
            Assert.That(okResult.Value, Is.TypeOf<ValidateTokenMetadataResponse>());
            var response = (ValidateTokenMetadataResponse)okResult.Value;
            Assert.That(response.IsValid, Is.True);
            Assert.That(response.ValidationResult!.Warnings, Is.Not.Empty);
        }
    }
}

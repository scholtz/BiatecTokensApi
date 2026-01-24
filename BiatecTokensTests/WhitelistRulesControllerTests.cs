using BiatecTokensApi.Controllers;
using BiatecTokensApi.Models.Whitelist;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System.Security.Claims;

namespace BiatecTokensTests
{
    [TestFixture]
    public class WhitelistRulesControllerTests
    {
        private Mock<IWhitelistRulesService> _serviceMock = null!;
        private Mock<ILogger<WhitelistRulesController>> _loggerMock = null!;
        private WhitelistRulesController _controller = null!;

        [SetUp]
        public void Setup()
        {
            _serviceMock = new Mock<IWhitelistRulesService>();
            _loggerMock = new Mock<ILogger<WhitelistRulesController>>();
            
            _controller = new WhitelistRulesController(_serviceMock.Object, _loggerMock.Object);
            
            // Setup authentication context
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, "TESTUSER123")
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var claimsPrincipal = new ClaimsPrincipal(identity);
            
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = claimsPrincipal }
            };
        }

        #region CreateRule Tests

        [Test]
        public async Task CreateRule_ValidRequest_ShouldReturnOk()
        {
            // Arrange
            var request = new CreateWhitelistRuleRequest
            {
                AssetId = 12345,
                Name = "Test Rule",
                RuleType = WhitelistRuleType.KycRequired,
                Configuration = new WhitelistRuleConfiguration { KycMandatory = true }
            };

            var expectedRule = new WhitelistRule
            {
                Id = "rule-123",
                AssetId = 12345,
                Name = "Test Rule",
                RuleType = WhitelistRuleType.KycRequired
            };

            _serviceMock.Setup(s => s.CreateRuleAsync(It.IsAny<CreateWhitelistRuleRequest>(), "TESTUSER123"))
                .ReturnsAsync(new WhitelistRuleResponse { Success = true, Rule = expectedRule });

            // Act
            var result = await _controller.CreateRule(request);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            var response = okResult!.Value as WhitelistRuleResponse;
            Assert.That(response!.Success, Is.True);
            Assert.That(response.Rule!.Name, Is.EqualTo("Test Rule"));
        }

        [Test]
        public async Task CreateRule_InvalidRequest_ShouldReturnBadRequest()
        {
            // Arrange
            var request = new CreateWhitelistRuleRequest
            {
                AssetId = 12345,
                Name = "Test Rule",
                RuleType = WhitelistRuleType.KycRequired,
                Configuration = new WhitelistRuleConfiguration()
            };

            _serviceMock.Setup(s => s.CreateRuleAsync(It.IsAny<CreateWhitelistRuleRequest>(), "TESTUSER123"))
                .ReturnsAsync(new WhitelistRuleResponse 
                { 
                    Success = false, 
                    ErrorMessage = "KYC configuration required" 
                });

            // Act
            var result = await _controller.CreateRule(request);

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        }

        #endregion

        #region UpdateRule Tests

        [Test]
        public async Task UpdateRule_ExistingRule_ShouldReturnOk()
        {
            // Arrange
            var request = new UpdateWhitelistRuleRequest
            {
                RuleId = "rule-123",
                Name = "Updated Name"
            };

            _serviceMock.Setup(s => s.UpdateRuleAsync(It.IsAny<UpdateWhitelistRuleRequest>(), "TESTUSER123"))
                .ReturnsAsync(new WhitelistRuleResponse 
                { 
                    Success = true, 
                    Rule = new WhitelistRule { Id = "rule-123", Name = "Updated Name" } 
                });

            // Act
            var result = await _controller.UpdateRule("rule-123", request);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
        }

        [Test]
        public async Task UpdateRule_NonExistingRule_ShouldReturnNotFound()
        {
            // Arrange
            var request = new UpdateWhitelistRuleRequest
            {
                RuleId = "non-existing",
                Name = "Test"
            };

            _serviceMock.Setup(s => s.UpdateRuleAsync(It.IsAny<UpdateWhitelistRuleRequest>(), "TESTUSER123"))
                .ReturnsAsync(new WhitelistRuleResponse 
                { 
                    Success = false, 
                    ErrorMessage = "Rule not found" 
                });

            // Act
            var result = await _controller.UpdateRule("non-existing", request);

            // Assert
            Assert.That(result, Is.InstanceOf<NotFoundObjectResult>());
        }

        #endregion

        #region GetRule Tests

        [Test]
        public async Task GetRule_ExistingRule_ShouldReturnOk()
        {
            // Arrange
            _serviceMock.Setup(s => s.GetRuleAsync("rule-123"))
                .ReturnsAsync(new WhitelistRuleResponse 
                { 
                    Success = true, 
                    Rule = new WhitelistRule { Id = "rule-123" } 
                });

            // Act
            var result = await _controller.GetRule("rule-123");

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
        }

        [Test]
        public async Task GetRule_NonExistingRule_ShouldReturnNotFound()
        {
            // Arrange
            _serviceMock.Setup(s => s.GetRuleAsync("non-existing"))
                .ReturnsAsync(new WhitelistRuleResponse 
                { 
                    Success = false, 
                    ErrorMessage = "Not found" 
                });

            // Act
            var result = await _controller.GetRule("non-existing");

            // Assert
            Assert.That(result, Is.InstanceOf<NotFoundObjectResult>());
        }

        #endregion

        #region ListRules Tests

        [Test]
        public async Task ListRules_ValidRequest_ShouldReturnOk()
        {
            // Arrange
            var rules = new List<WhitelistRule>
            {
                new WhitelistRule { Id = "rule-1", AssetId = 12345 },
                new WhitelistRule { Id = "rule-2", AssetId = 12345 }
            };

            _serviceMock.Setup(s => s.ListRulesAsync(It.IsAny<ListWhitelistRulesRequest>()))
                .ReturnsAsync(new WhitelistRulesListResponse 
                { 
                    Success = true, 
                    Rules = rules,
                    TotalCount = 2
                });

            // Act
            var result = await _controller.ListRules(12345);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            var response = okResult!.Value as WhitelistRulesListResponse;
            Assert.That(response!.Rules.Count, Is.EqualTo(2));
        }

        #endregion

        #region DeleteRule Tests

        [Test]
        public async Task DeleteRule_ExistingRule_ShouldReturnOk()
        {
            // Arrange
            _serviceMock.Setup(s => s.DeleteRuleAsync("rule-123", "TESTUSER123"))
                .ReturnsAsync(new WhitelistRuleResponse { Success = true });

            // Act
            var result = await _controller.DeleteRule("rule-123");

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
        }

        #endregion

        #region ApplyRule Tests

        [Test]
        public async Task ApplyRule_ValidRequest_ShouldReturnOk()
        {
            // Arrange
            var request = new ApplyWhitelistRuleRequest
            {
                RuleId = "rule-123",
                ApplyToExisting = true
            };

            _serviceMock.Setup(s => s.ApplyRuleAsync(It.IsAny<ApplyWhitelistRuleRequest>(), "TESTUSER123"))
                .ReturnsAsync(new ApplyRuleResponse 
                { 
                    Success = true, 
                    EntriesEvaluated = 10,
                    EntriesPassed = 8,
                    EntriesFailed = 2
                });

            // Act
            var result = await _controller.ApplyRule("rule-123", request);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
        }

        #endregion

        #region ValidateAgainstRules Tests

        [Test]
        public async Task ValidateAgainstRules_ValidRequest_ShouldReturnOk()
        {
            // Arrange
            var request = new ValidateAgainstRulesRequest
            {
                AssetId = 12345,
                Address = "TESTADDRESS"
            };

            _serviceMock.Setup(s => s.ValidateAgainstRulesAsync(It.IsAny<ValidateAgainstRulesRequest>()))
                .ReturnsAsync(new ValidateAgainstRulesResponse 
                { 
                    Success = true, 
                    IsValid = true,
                    RulesEvaluated = 3,
                    RulesPassed = 3
                });

            // Act
            var result = await _controller.ValidateAgainstRules(request);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
        }

        #endregion
    }
}

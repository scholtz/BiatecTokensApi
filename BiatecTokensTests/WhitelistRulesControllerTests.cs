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
    public class WhitelistRulesControllerTests
    {
        private Mock<IWhitelistRulesService> _rulesServiceMock;
        private Mock<ILogger<WhitelistRulesController>> _loggerMock;
        private WhitelistRulesController _controller;
        private const string TestUserAddress = "TESTUSER1234567890123456789012345678901234567890123456";

        [SetUp]
        public void Setup()
        {
            _rulesServiceMock = new Mock<IWhitelistRulesService>();
            _loggerMock = new Mock<ILogger<WhitelistRulesController>>();
            _controller = new WhitelistRulesController(_rulesServiceMock.Object, _loggerMock.Object);

            // Set up authenticated user context
            var claims = new List<Claim>
            {
                new Claim("address", TestUserAddress),
                new Claim(ClaimTypes.NameIdentifier, TestUserAddress)
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            };
        }

        #region Create Rule Tests

        [Test]
        public async Task CreateRule_ValidRequest_ShouldReturnOk()
        {
            // Arrange
            var request = new CreateWhitelistRuleRequest
            {
                AssetId = 12345,
                Name = "Test Rule",
                Description = "Test Description",
                RuleType = WhitelistRuleType.AutoRevokeExpired,
                IsActive = true,
                Priority = 100
            };

            var response = new WhitelistRuleResponse
            {
                Success = true,
                Rule = new WhitelistRule
                {
                    AssetId = 12345,
                    Name = "Test Rule",
                    RuleType = WhitelistRuleType.AutoRevokeExpired
                }
            };

            _rulesServiceMock.Setup(s => s.CreateRuleAsync(It.IsAny<CreateWhitelistRuleRequest>(), TestUserAddress))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.CreateRule(request);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            Assert.That(okResult?.Value, Is.InstanceOf<WhitelistRuleResponse>());
            var ruleResponse = okResult?.Value as WhitelistRuleResponse;
            Assert.That(ruleResponse?.Success, Is.True);

            _rulesServiceMock.Verify(s => s.CreateRuleAsync(request, TestUserAddress), Times.Once);
        }

        [Test]
        public async Task CreateRule_ServiceFailure_ShouldReturnBadRequest()
        {
            // Arrange
            var request = new CreateWhitelistRuleRequest
            {
                AssetId = 12345,
                Name = "Test Rule",
                RuleType = WhitelistRuleType.AutoRevokeExpired
            };

            var response = new WhitelistRuleResponse
            {
                Success = false,
                ErrorMessage = "Failed to create rule"
            };

            _rulesServiceMock.Setup(s => s.CreateRuleAsync(It.IsAny<CreateWhitelistRuleRequest>(), TestUserAddress))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.CreateRule(request);

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task CreateRule_NoAuthentication_ShouldReturnUnauthorized()
        {
            // Arrange
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() }
            };

            var request = new CreateWhitelistRuleRequest
            {
                AssetId = 12345,
                Name = "Test Rule",
                RuleType = WhitelistRuleType.AutoRevokeExpired
            };

            // Act
            var result = await _controller.CreateRule(request);

            // Assert
            Assert.That(result, Is.InstanceOf<UnauthorizedObjectResult>());
        }

        #endregion

        #region Update Rule Tests

        [Test]
        public async Task UpdateRule_ValidRequest_ShouldReturnOk()
        {
            // Arrange
            var request = new UpdateWhitelistRuleRequest
            {
                RuleId = "rule-123",
                Name = "Updated Name",
                IsActive = false
            };

            var response = new WhitelistRuleResponse
            {
                Success = true,
                Rule = new WhitelistRule
                {
                    Id = "rule-123",
                    Name = "Updated Name",
                    IsActive = false
                }
            };

            _rulesServiceMock.Setup(s => s.UpdateRuleAsync(It.IsAny<UpdateWhitelistRuleRequest>(), TestUserAddress))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.UpdateRule(request);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            var ruleResponse = okResult?.Value as WhitelistRuleResponse;
            Assert.That(ruleResponse?.Success, Is.True);

            _rulesServiceMock.Verify(s => s.UpdateRuleAsync(request, TestUserAddress), Times.Once);
        }

        [Test]
        public async Task UpdateRule_RuleNotFound_ShouldReturnNotFound()
        {
            // Arrange
            var request = new UpdateWhitelistRuleRequest
            {
                RuleId = "non-existing-id",
                Name = "Updated Name"
            };

            var response = new WhitelistRuleResponse
            {
                Success = false,
                ErrorMessage = "Rule with ID non-existing-id not found"
            };

            _rulesServiceMock.Setup(s => s.UpdateRuleAsync(It.IsAny<UpdateWhitelistRuleRequest>(), TestUserAddress))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.UpdateRule(request);

            // Assert
            Assert.That(result, Is.InstanceOf<NotFoundObjectResult>());
        }

        [Test]
        public async Task UpdateRule_NoAuthentication_ShouldReturnUnauthorized()
        {
            // Arrange
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() }
            };

            var request = new UpdateWhitelistRuleRequest
            {
                RuleId = "rule-123",
                Name = "Updated Name"
            };

            // Act
            var result = await _controller.UpdateRule(request);

            // Assert
            Assert.That(result, Is.InstanceOf<UnauthorizedObjectResult>());
        }

        #endregion

        #region List Rules Tests

        [Test]
        public async Task ListRules_ValidRequest_ShouldReturnOk()
        {
            // Arrange
            var assetId = 12345UL;
            var response = new WhitelistRulesListResponse
            {
                Success = true,
                Rules = new List<WhitelistRule>
                {
                    new WhitelistRule { AssetId = assetId, Name = "Rule 1", RuleType = WhitelistRuleType.AutoRevokeExpired },
                    new WhitelistRule { AssetId = assetId, Name = "Rule 2", RuleType = WhitelistRuleType.RequireKycForActive }
                },
                TotalCount = 2,
                Page = 1,
                PageSize = 20,
                TotalPages = 1
            };

            _rulesServiceMock.Setup(s => s.ListRulesAsync(It.IsAny<ListWhitelistRulesRequest>()))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.ListRules(assetId);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            var listResponse = okResult?.Value as WhitelistRulesListResponse;
            Assert.That(listResponse?.Success, Is.True);
            Assert.That(listResponse?.Rules, Has.Count.EqualTo(2));
        }

        [Test]
        public async Task ListRules_WithFilters_ShouldPassCorrectParameters()
        {
            // Arrange
            var assetId = 12345UL;
            var ruleType = WhitelistRuleType.NetworkKycRequirement;
            var isActive = true;
            var network = "voimain-v1.0";

            _rulesServiceMock.Setup(s => s.ListRulesAsync(It.Is<ListWhitelistRulesRequest>(r =>
                r.AssetId == assetId &&
                r.RuleType == ruleType &&
                r.IsActive == isActive &&
                r.Network == network)))
                .ReturnsAsync(new WhitelistRulesListResponse { Success = true });

            // Act
            await _controller.ListRules(assetId, ruleType, isActive, network);

            // Assert
            _rulesServiceMock.Verify(s => s.ListRulesAsync(It.Is<ListWhitelistRulesRequest>(r =>
                r.AssetId == assetId &&
                r.RuleType == ruleType &&
                r.IsActive == isActive &&
                r.Network == network)), Times.Once);
        }

        [Test]
        public async Task ListRules_WithPagination_ShouldPassCorrectParameters()
        {
            // Arrange
            var assetId = 12345UL;
            var page = 2;
            var pageSize = 50;

            _rulesServiceMock.Setup(s => s.ListRulesAsync(It.Is<ListWhitelistRulesRequest>(r =>
                r.Page == page && r.PageSize == pageSize)))
                .ReturnsAsync(new WhitelistRulesListResponse { Success = true });

            // Act
            await _controller.ListRules(assetId, page: page, pageSize: pageSize);

            // Assert
            _rulesServiceMock.Verify(s => s.ListRulesAsync(It.Is<ListWhitelistRulesRequest>(r =>
                r.Page == page && r.PageSize == pageSize)), Times.Once);
        }

        [Test]
        public async Task ListRules_ServiceFailure_ShouldReturnInternalServerError()
        {
            // Arrange
            var assetId = 12345UL;
            var response = new WhitelistRulesListResponse
            {
                Success = false,
                ErrorMessage = "Internal error"
            };

            _rulesServiceMock.Setup(s => s.ListRulesAsync(It.IsAny<ListWhitelistRulesRequest>()))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.ListRules(assetId);

            // Assert
            Assert.That(result, Is.InstanceOf<ObjectResult>());
            var objectResult = result as ObjectResult;
            Assert.That(objectResult?.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
        }

        #endregion

        #region Apply Rule Tests

        [Test]
        public async Task ApplyRule_ValidRequest_ShouldReturnOk()
        {
            // Arrange
            var request = new ApplyWhitelistRuleRequest
            {
                RuleId = "rule-123",
                DryRun = false
            };

            var response = new ApplyWhitelistRuleResponse
            {
                Success = true,
                Result = new RuleApplicationResult
                {
                    Success = true,
                    AffectedEntriesCount = 5
                }
            };

            _rulesServiceMock.Setup(s => s.ApplyRuleAsync(It.IsAny<ApplyWhitelistRuleRequest>(), TestUserAddress))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.ApplyRule(request);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            var applyResponse = okResult?.Value as ApplyWhitelistRuleResponse;
            Assert.That(applyResponse?.Success, Is.True);
            Assert.That(applyResponse?.Result?.AffectedEntriesCount, Is.EqualTo(5));

            _rulesServiceMock.Verify(s => s.ApplyRuleAsync(request, TestUserAddress), Times.Once);
        }

        [Test]
        public async Task ApplyRule_DryRun_ShouldNotMakeChanges()
        {
            // Arrange
            var request = new ApplyWhitelistRuleRequest
            {
                RuleId = "rule-123",
                DryRun = true
            };

            var response = new ApplyWhitelistRuleResponse
            {
                Success = true,
                Result = new RuleApplicationResult
                {
                    Success = true,
                    AffectedEntriesCount = 5
                }
            };

            _rulesServiceMock.Setup(s => s.ApplyRuleAsync(It.IsAny<ApplyWhitelistRuleRequest>(), TestUserAddress))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.ApplyRule(request);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            _rulesServiceMock.Verify(s => s.ApplyRuleAsync(It.Is<ApplyWhitelistRuleRequest>(r => r.DryRun == true), TestUserAddress), Times.Once);
        }

        [Test]
        public async Task ApplyRule_RuleNotFound_ShouldReturnNotFound()
        {
            // Arrange
            var request = new ApplyWhitelistRuleRequest
            {
                RuleId = "non-existing-id",
                DryRun = false
            };

            var response = new ApplyWhitelistRuleResponse
            {
                Success = false,
                ErrorMessage = "Rule with ID non-existing-id not found"
            };

            _rulesServiceMock.Setup(s => s.ApplyRuleAsync(It.IsAny<ApplyWhitelistRuleRequest>(), TestUserAddress))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.ApplyRule(request);

            // Assert
            Assert.That(result, Is.InstanceOf<NotFoundObjectResult>());
        }

        [Test]
        public async Task ApplyRule_NoAuthentication_ShouldReturnUnauthorized()
        {
            // Arrange
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() }
            };

            var request = new ApplyWhitelistRuleRequest
            {
                RuleId = "rule-123",
                DryRun = false
            };

            // Act
            var result = await _controller.ApplyRule(request);

            // Assert
            Assert.That(result, Is.InstanceOf<UnauthorizedObjectResult>());
        }

        #endregion

        #region Delete Rule Tests

        [Test]
        public async Task DeleteRule_ValidRequest_ShouldReturnOk()
        {
            // Arrange
            var ruleId = "rule-123";
            var response = new DeleteWhitelistRuleResponse
            {
                Success = true,
                RuleId = ruleId
            };

            _rulesServiceMock.Setup(s => s.DeleteRuleAsync(It.IsAny<DeleteWhitelistRuleRequest>(), TestUserAddress))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.DeleteRule(ruleId);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            var deleteResponse = okResult?.Value as DeleteWhitelistRuleResponse;
            Assert.That(deleteResponse?.Success, Is.True);
            Assert.That(deleteResponse?.RuleId, Is.EqualTo(ruleId));

            _rulesServiceMock.Verify(s => s.DeleteRuleAsync(It.Is<DeleteWhitelistRuleRequest>(r => r.RuleId == ruleId), TestUserAddress), Times.Once);
        }

        [Test]
        public async Task DeleteRule_RuleNotFound_ShouldReturnNotFound()
        {
            // Arrange
            var ruleId = "non-existing-id";
            var response = new DeleteWhitelistRuleResponse
            {
                Success = false,
                ErrorMessage = "Rule with ID non-existing-id not found"
            };

            _rulesServiceMock.Setup(s => s.DeleteRuleAsync(It.IsAny<DeleteWhitelistRuleRequest>(), TestUserAddress))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.DeleteRule(ruleId);

            // Assert
            Assert.That(result, Is.InstanceOf<NotFoundObjectResult>());
        }

        [Test]
        public async Task DeleteRule_NoAuthentication_ShouldReturnUnauthorized()
        {
            // Arrange
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() }
            };

            var ruleId = "rule-123";

            // Act
            var result = await _controller.DeleteRule(ruleId);

            // Assert
            Assert.That(result, Is.InstanceOf<UnauthorizedObjectResult>());
        }

        #endregion

        #region Get Audit Log Tests

        [Test]
        public async Task GetAuditLog_ValidRequest_ShouldReturnOk()
        {
            // Arrange
            var assetId = 12345UL;
            var response = new WhitelistRuleAuditLogResponse
            {
                Success = true,
                Entries = new List<WhitelistRuleAuditLog>
                {
                    new WhitelistRuleAuditLog
                    {
                        AssetId = assetId,
                        RuleId = "rule-123",
                        RuleName = "Test Rule",
                        ActionType = RuleAuditActionType.Create,
                        PerformedBy = "USER1"
                    }
                },
                TotalCount = 1,
                Page = 1,
                PageSize = 50,
                TotalPages = 1
            };

            _rulesServiceMock.Setup(s => s.GetAuditLogsAsync(assetId, null, null, null, null, 1, 50))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.GetAuditLog(assetId);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            var auditResponse = okResult?.Value as WhitelistRuleAuditLogResponse;
            Assert.That(auditResponse?.Success, Is.True);
            Assert.That(auditResponse?.Entries, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task GetAuditLog_WithFilters_ShouldPassCorrectParameters()
        {
            // Arrange
            var assetId = 12345UL;
            var ruleId = "rule-123";
            var actionType = RuleAuditActionType.Apply;
            var fromDate = DateTime.UtcNow.AddDays(-7);
            var toDate = DateTime.UtcNow;

            _rulesServiceMock.Setup(s => s.GetAuditLogsAsync(assetId, ruleId, actionType, fromDate, toDate, 1, 50))
                .ReturnsAsync(new WhitelistRuleAuditLogResponse { Success = true });

            // Act
            await _controller.GetAuditLog(assetId, ruleId, actionType, fromDate, toDate);

            // Assert
            _rulesServiceMock.Verify(s => s.GetAuditLogsAsync(assetId, ruleId, actionType, fromDate, toDate, 1, 50), Times.Once);
        }

        [Test]
        public async Task GetAuditLog_WithPagination_ShouldPassCorrectParameters()
        {
            // Arrange
            var assetId = 12345UL;
            var page = 2;
            var pageSize = 100;

            _rulesServiceMock.Setup(s => s.GetAuditLogsAsync(assetId, null, null, null, null, page, pageSize))
                .ReturnsAsync(new WhitelistRuleAuditLogResponse { Success = true });

            // Act
            await _controller.GetAuditLog(assetId, page: page, pageSize: pageSize);

            // Assert
            _rulesServiceMock.Verify(s => s.GetAuditLogsAsync(assetId, null, null, null, null, page, pageSize), Times.Once);
        }

        [Test]
        public async Task GetAuditLog_ServiceFailure_ShouldReturnInternalServerError()
        {
            // Arrange
            var assetId = 12345UL;
            var response = new WhitelistRuleAuditLogResponse
            {
                Success = false,
                ErrorMessage = "Internal error"
            };

            _rulesServiceMock.Setup(s => s.GetAuditLogsAsync(assetId, null, null, null, null, 1, 50))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.GetAuditLog(assetId);

            // Assert
            Assert.That(result, Is.InstanceOf<ObjectResult>());
            var objectResult = result as ObjectResult;
            Assert.That(objectResult?.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
        }

        #endregion
    }
}

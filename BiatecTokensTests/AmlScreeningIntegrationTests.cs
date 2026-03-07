using BiatecTokensApi.Controllers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Aml;
using BiatecTokensApi.Models.ComplianceOrchestration;
using BiatecTokensApi.Repositories;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace BiatecTokensTests
{
    /// <summary>
    /// Integration tests for AML screening flow.
    /// Tests the AmlController → AmlService → IAmlProvider → AmlRepository pipeline.
    /// Covers /aml/screen, /aml/status/{userId}, and /aml/report/{userId} endpoints.
    /// </summary>
    [TestFixture]
    public class AmlScreeningIntegrationTests
    {
        private AmlRepository _repository = null!;
        private Mock<IAmlProvider> _providerMock = null!;
        private AmlService _service = null!;
        private AmlController _controller = null!;

        [SetUp]
        public void Setup()
        {
            _repository = new AmlRepository(new Mock<ILogger<AmlRepository>>().Object);
            _providerMock = new Mock<IAmlProvider>();
            _service = new AmlService(
                _repository,
                _providerMock.Object,
                new Mock<ILogger<AmlService>>().Object);
            _controller = new AmlController(_service, new Mock<ILogger<AmlController>>().Object);
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { TraceIdentifier = "integration-test-trace" }
            };
        }

        // =========================================================
        // /aml/screen endpoint
        // =========================================================

        [Test]
        public async Task Screen_ClearedUser_ShouldReturnOkWithClearedStatus()
        {
            // Arrange
            var userId = "user-int-cleared";
            _providerMock.Setup(p => p.ScreenSubjectAsync(userId, It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()))
                .ReturnsAsync(("AML-INT-001", ComplianceDecisionState.Approved, (string?)null, (string?)null));

            var request = new AmlScreenRequest { UserId = userId };

            // Act
            var result = await _controller.Screen(request);

            // Assert
            var okResult = result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);
            var response = okResult!.Value as AmlScreenResponse;
            Assert.That(response, Is.Not.Null);
            Assert.That(response!.Success, Is.True);
            Assert.That(response.Status, Is.EqualTo(AmlScreeningStatus.Cleared));
            Assert.That(response.RiskLevel, Is.EqualTo(AmlRiskLevel.Low));
        }

        [Test]
        public async Task Screen_SanctionedUser_ShouldReturnOkWithSanctionsMatchStatus()
        {
            // Arrange
            var userId = "user-int-sanctioned";
            _providerMock.Setup(p => p.ScreenSubjectAsync(userId, It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()))
                .ReturnsAsync(("AML-INT-002", ComplianceDecisionState.Rejected, "SANCTIONS_MATCH", (string?)null));

            var request = new AmlScreenRequest { UserId = userId };

            // Act
            var result = await _controller.Screen(request);

            // Assert
            var okResult = result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);
            var response = okResult!.Value as AmlScreenResponse;
            Assert.That(response, Is.Not.Null);
            Assert.That(response!.Status, Is.EqualTo(AmlScreeningStatus.SanctionsMatch));
            Assert.That(response.RiskLevel, Is.EqualTo(AmlRiskLevel.High));
        }

        [Test]
        public async Task Screen_EmptyUserId_ShouldReturnBadRequest()
        {
            // Arrange
            var request = new AmlScreenRequest { UserId = "" };

            // Act
            var result = await _controller.Screen(request);

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task Screen_ProviderError_ShouldStillReturnOkWithErrorStatus()
        {
            // Arrange
            var userId = "user-int-error";
            _providerMock.Setup(p => p.ScreenSubjectAsync(userId, It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()))
                .ReturnsAsync(("AML-INT-ERR", ComplianceDecisionState.Error, "PROVIDER_TIMEOUT", "Timeout"));

            var request = new AmlScreenRequest { UserId = userId };

            // Act
            var result = await _controller.Screen(request);

            // Assert
            var okResult = result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);
            var response = okResult!.Value as AmlScreenResponse;
            Assert.That(response!.Status, Is.EqualTo(AmlScreeningStatus.Error));
        }

        [Test]
        public async Task Screen_WithMetadata_ShouldPassMetadataToProvider()
        {
            // Arrange
            var userId = "user-int-meta";
            var metadata = new Dictionary<string, string> { ["country"] = "US", ["risk_flag"] = "false" };

            _providerMock.Setup(p => p.ScreenSubjectAsync(userId, metadata, It.IsAny<string>()))
                .ReturnsAsync(("AML-INT-META", ComplianceDecisionState.Approved, (string?)null, (string?)null));

            var request = new AmlScreenRequest { UserId = userId, Metadata = metadata };

            // Act
            await _controller.Screen(request);

            // Assert
            _providerMock.Verify(p => p.ScreenSubjectAsync(userId, metadata, It.IsAny<string>()), Times.Once);
        }

        // =========================================================
        // /aml/status/{userId}
        // =========================================================

        [Test]
        public async Task GetStatus_AfterScreening_ShouldReturnPersistedStatus()
        {
            // Arrange — first screen the user
            var userId = "user-int-status";
            _providerMock.Setup(p => p.ScreenSubjectAsync(userId, It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()))
                .ReturnsAsync(("AML-INT-ST-001", ComplianceDecisionState.Approved, (string?)null, (string?)null));

            await _service.ScreenUserAsync(userId, new Dictionary<string, string>(), "corr-status-test");

            // Act
            var result = await _controller.GetStatus(userId);

            // Assert
            var okResult = result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);
            var response = okResult!.Value as AmlStatusResponse;
            Assert.That(response, Is.Not.Null);
            Assert.That(response!.Success, Is.True);
            Assert.That(response.Status, Is.EqualTo(AmlScreeningStatus.Cleared));
            Assert.That(response.RiskLevel, Is.EqualTo(AmlRiskLevel.Low));
        }

        [Test]
        public async Task GetStatus_NotScreenedUser_ShouldReturnNotScreenedStatus()
        {
            // Arrange — user has never been screened
            var userId = "user-int-not-screened";

            // Act
            var result = await _controller.GetStatus(userId);

            // Assert
            var okResult = result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);
            var response = okResult!.Value as AmlStatusResponse;
            Assert.That(response!.Status, Is.EqualTo(AmlScreeningStatus.NotScreened));
        }

        [Test]
        public async Task GetStatus_AfterRescreening_ShouldReturnUpdatedStatus()
        {
            // Arrange — screen twice
            var userId = "user-int-rescreen";
            _providerMock.SetupSequence(p => p.ScreenSubjectAsync(userId, It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()))
                .ReturnsAsync(("AML-RS-001", ComplianceDecisionState.Approved, (string?)null, (string?)null))
                .ReturnsAsync(("AML-RS-002", ComplianceDecisionState.NeedsReview, "REVIEW_REQUIRED", (string?)null));

            await _service.ScreenUserAsync(userId, new Dictionary<string, string>(), "corr-rs-1");
            await _service.ScreenUserAsync(userId, new Dictionary<string, string>(), "corr-rs-2");

            // Act
            var result = await _controller.GetStatus(userId);

            // Assert
            var okResult = result as OkObjectResult;
            var response = okResult!.Value as AmlStatusResponse;
            Assert.That(response!.Status, Is.EqualTo(AmlScreeningStatus.NeedsReview));
        }

        // =========================================================
        // /aml/report/{userId}
        // =========================================================

        [Test]
        public async Task GetReport_AfterMultipleScreenings_ShouldReturnFullHistory()
        {
            // Arrange — screen 3 times
            var userId = "user-int-report";
            _providerMock.SetupSequence(p => p.ScreenSubjectAsync(userId, It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()))
                .ReturnsAsync(("AML-RPT-001", ComplianceDecisionState.Approved, (string?)null, (string?)null))
                .ReturnsAsync(("AML-RPT-002", ComplianceDecisionState.NeedsReview, "REVIEW_REQUIRED", (string?)null))
                .ReturnsAsync(("AML-RPT-003", ComplianceDecisionState.Approved, (string?)null, (string?)null));

            await _service.ScreenUserAsync(userId, new Dictionary<string, string>(), "corr-rpt-1");
            await _service.ScreenUserAsync(userId, new Dictionary<string, string>(), "corr-rpt-2");
            await _service.ScreenUserAsync(userId, new Dictionary<string, string>(), "corr-rpt-3");

            // Act
            var result = await _controller.GetReport(userId);

            // Assert
            var okResult = result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);
            var response = okResult!.Value as AmlReportResponse;
            Assert.That(response, Is.Not.Null);
            Assert.That(response!.Success, Is.True);
            Assert.That(response.UserId, Is.EqualTo(userId));
            Assert.That(response.GeneratedAt, Is.GreaterThan(DateTime.UtcNow.AddMinutes(-1)));
            Assert.That(response.ComplianceSummary, Is.Not.Null.Or.Empty);
        }

        [Test]
        public async Task GetReport_NotScreenedUser_ShouldReturnEmptyReport()
        {
            // Arrange
            var userId = "user-int-no-report";

            // Act
            var result = await _controller.GetReport(userId);

            // Assert
            var okResult = result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);
            var response = okResult!.Value as AmlReportResponse;
            Assert.That(response!.ScreeningHistory, Is.Empty);
            Assert.That(response.ComplianceSummary, Does.Contain("No AML screening"));
        }

        // =========================================================
        // Full screening lifecycle
        // =========================================================

        [Test]
        public async Task FullLifecycle_ClearedUserRescreenedWithSanctions_ShouldBlockUser()
        {
            // Arrange
            var userId = "user-int-lifecycle";

            // First screening: cleared
            _providerMock.SetupSequence(p => p.ScreenSubjectAsync(userId, It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()))
                .ReturnsAsync(("AML-LC-001", ComplianceDecisionState.Approved, (string?)null, (string?)null))
                .ReturnsAsync(("AML-LC-002", ComplianceDecisionState.Rejected, "SANCTIONS_MATCH", (string?)null));

            // Act — first screening
            await _service.ScreenUserAsync(userId, new Dictionary<string, string>(), "corr-lc-1");

            var clearedResult = await _service.IsUserClearedAsync(userId);
            Assert.That(clearedResult, Is.True, "User should be cleared after first screening");

            // Act — re-screening with sanctions match
            await _service.ScreenUserAsync(userId, new Dictionary<string, string>(), "corr-lc-2");

            var blockedResult = await _service.IsUserClearedAsync(userId);
            Assert.That(blockedResult, Is.False, "User should be blocked after sanctions match");
        }

        [Test]
        public async Task FullLifecycle_PepMatchThenManualClear_ShouldUpdateStatus()
        {
            // Arrange
            var userId = "user-int-pep-lifecycle";

            _providerMock.Setup(p => p.ScreenSubjectAsync(userId, It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()))
                .ReturnsAsync(("AML-PEP-LC-001", ComplianceDecisionState.Rejected, "PEP_MATCH", (string?)null));

            // Act — initial screening gives PEP match
            await _service.ScreenUserAsync(userId, new Dictionary<string, string>(), "corr-pep-lc");
            var statusBeforeWebhook = await _service.GetStatusAsync(userId);
            Assert.That(statusBeforeWebhook.Status, Is.EqualTo(AmlScreeningStatus.PepMatch));

            // Act — continuous monitoring webhook clears after review
            var clearPayload = new AmlWebhookPayload
            {
                ProviderReferenceId = "AML-PEP-LC-001",
                AlertType = "CLEARED",
                Status = "CLEARED",
                RiskLevel = "LOW",
                Timestamp = DateTime.UtcNow
            };
            await _service.HandleWebhookAsync(clearPayload, null);

            var statusAfterWebhook = await _service.GetStatusAsync(userId);
            Assert.That(statusAfterWebhook.Status, Is.EqualTo(AmlScreeningStatus.Cleared));
        }

        // =========================================================
        // MockAmlProvider integration
        // =========================================================

        [Test]
        public async Task MockAmlProvider_SanctionsFlag_ShouldReturnRejected()
        {
            // Arrange
            var mockProvider = new MockAmlProvider(new Mock<ILogger<MockAmlProvider>>().Object);
            var metadata = new Dictionary<string, string> { ["sanctions_flag"] = "true" };

            // Act
            var (refId, state, reasonCode, errorMessage) = await mockProvider.ScreenSubjectAsync(
                "user-sanctions-mock", metadata, "corr-mock");

            // Assert
            Assert.That(state, Is.EqualTo(ComplianceDecisionState.Rejected));
            Assert.That(reasonCode, Is.EqualTo("SANCTIONS_MATCH"));
        }

        [Test]
        public async Task MockAmlProvider_ReviewFlag_ShouldReturnNeedsReview()
        {
            // Arrange
            var mockProvider = new MockAmlProvider(new Mock<ILogger<MockAmlProvider>>().Object);
            var metadata = new Dictionary<string, string> { ["review_flag"] = "true" };

            // Act
            var (refId, state, reasonCode, errorMessage) = await mockProvider.ScreenSubjectAsync(
                "user-review-mock", metadata, "corr-mock");

            // Assert
            Assert.That(state, Is.EqualTo(ComplianceDecisionState.NeedsReview));
            Assert.That(reasonCode, Is.EqualTo("REVIEW_REQUIRED"));
        }

        [Test]
        public async Task MockAmlProvider_NoFlags_ShouldReturnApproved()
        {
            // Arrange
            var mockProvider = new MockAmlProvider(new Mock<ILogger<MockAmlProvider>>().Object);

            // Act
            var (refId, state, reasonCode, errorMessage) = await mockProvider.ScreenSubjectAsync(
                "user-clean-mock", new Dictionary<string, string>(), "corr-mock");

            // Assert
            Assert.That(state, Is.EqualTo(ComplianceDecisionState.Approved));
            Assert.That(errorMessage, Is.Null);
        }

        [Test]
        public async Task MockAmlProvider_SimulateTimeout_ShouldReturnError()
        {
            // Arrange
            var mockProvider = new MockAmlProvider(new Mock<ILogger<MockAmlProvider>>().Object);
            var metadata = new Dictionary<string, string> { ["simulate_timeout"] = "true" };

            // Act
            var (refId, state, reasonCode, errorMessage) = await mockProvider.ScreenSubjectAsync(
                "user-timeout-mock", metadata, "corr-mock");

            // Assert
            Assert.That(state, Is.EqualTo(ComplianceDecisionState.Error));
            Assert.That(reasonCode, Is.EqualTo("PROVIDER_TIMEOUT"));
        }
    }
}

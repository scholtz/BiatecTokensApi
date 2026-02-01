using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Models.Metering;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging;
using Moq;

namespace BiatecTokensTests
{
    [TestFixture]
    public class ComplianceReportTests
    {
        private Mock<IComplianceRepository> _repositoryMock;
        private Mock<ILogger<ComplianceService>> _loggerMock;
        private Mock<ISubscriptionMeteringService> _meteringServiceMock;
        private ComplianceService _service;

        [SetUp]
        public void Setup()
        {
            _repositoryMock = new Mock<IComplianceRepository>();
            var whitelistServiceMock = new Mock<IWhitelistService>();
            _loggerMock = new Mock<ILogger<ComplianceService>>();
            _meteringServiceMock = new Mock<ISubscriptionMeteringService>();
            _service = new ComplianceService(_repositoryMock.Object, whitelistServiceMock.Object, _loggerMock.Object, _meteringServiceMock.Object, Mock.Of<IWebhookService>());
        }

        #region GetComplianceReportAsync Tests

        [Test]
        public async Task GetComplianceReportAsync_ValidRequest_ShouldSucceed()
        {
            // Arrange
            var request = new GetTokenComplianceReportRequest
            {
                Network = "voimain-v1.0",
                Page = 1,
                PageSize = 10
            };
            var requestedBy = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";

            var metadataList = new List<ComplianceMetadata>
            {
                new ComplianceMetadata
                {
                    AssetId = 12345,
                    Network = "voimain-v1.0",
                    ComplianceStatus = ComplianceStatus.Compliant,
                    VerificationStatus = VerificationStatus.Verified,
                    Jurisdiction = "US",
                    RegulatoryFramework = "SEC Reg D",
                    KycProvider = "Sumsub"
                }
            };

            _repositoryMock.Setup(r => r.ListMetadataAsync(It.IsAny<ListComplianceMetadataRequest>()))
                .ReturnsAsync(metadataList);
            _repositoryMock.Setup(r => r.GetMetadataCountAsync(It.IsAny<ListComplianceMetadataRequest>()))
                .ReturnsAsync(1);
            _repositoryMock.Setup(r => r.GetAuditLogAsync(It.IsAny<GetComplianceAuditLogRequest>()))
                .ReturnsAsync(new List<ComplianceAuditLogEntry>());

            // Act
            var result = await _service.GetComplianceReportAsync(request, requestedBy);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Tokens, Is.Not.Null);
            Assert.That(result.Tokens.Count, Is.EqualTo(1));
            Assert.That(result.Tokens[0].AssetId, Is.EqualTo(12345));
            Assert.That(result.Tokens[0].Network, Is.EqualTo("voimain-v1.0"));
            Assert.That(result.NetworkFilter, Is.EqualTo("voimain-v1.0"));
            Assert.That(result.SubscriptionInfo, Is.Not.Null);
        }

        [Test]
        public async Task GetComplianceReportAsync_SpecificAssetId_ShouldFilterCorrectly()
        {
            // Arrange
            var request = new GetTokenComplianceReportRequest
            {
                AssetId = 12345,
                Page = 1,
                PageSize = 10
            };
            var requestedBy = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";

            var metadataList = new List<ComplianceMetadata>
            {
                new ComplianceMetadata
                {
                    AssetId = 12345,
                    Network = "voimain-v1.0",
                    ComplianceStatus = ComplianceStatus.Compliant,
                    VerificationStatus = VerificationStatus.Verified
                },
                new ComplianceMetadata
                {
                    AssetId = 67890,
                    Network = "voimain-v1.0",
                    ComplianceStatus = ComplianceStatus.Compliant
                }
            };

            _repositoryMock.Setup(r => r.ListMetadataAsync(It.IsAny<ListComplianceMetadataRequest>()))
                .ReturnsAsync(metadataList);
            _repositoryMock.Setup(r => r.GetMetadataCountAsync(It.IsAny<ListComplianceMetadataRequest>()))
                .ReturnsAsync(2);
            _repositoryMock.Setup(r => r.GetAuditLogAsync(It.IsAny<GetComplianceAuditLogRequest>()))
                .ReturnsAsync(new List<ComplianceAuditLogEntry>());

            // Act
            var result = await _service.GetComplianceReportAsync(request, requestedBy);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Tokens.Count, Is.EqualTo(1));
            Assert.That(result.Tokens[0].AssetId, Is.EqualTo(12345));
        }

        [Test]
        public async Task GetComplianceReportAsync_VOINetwork_ShouldCalculateHealthScore()
        {
            // Arrange
            var request = new GetTokenComplianceReportRequest
            {
                Network = "voimain-v1.0",
                Page = 1,
                PageSize = 10
            };
            var requestedBy = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";

            var metadataList = new List<ComplianceMetadata>
            {
                new ComplianceMetadata
                {
                    AssetId = 12345,
                    Network = "voimain-v1.0",
                    ComplianceStatus = ComplianceStatus.Compliant,
                    VerificationStatus = VerificationStatus.Verified,
                    RegulatoryFramework = "MICA",
                    KycProvider = "Sumsub",
                    Jurisdiction = "EU"
                }
            };

            _repositoryMock.Setup(r => r.ListMetadataAsync(It.IsAny<ListComplianceMetadataRequest>()))
                .ReturnsAsync(metadataList);
            _repositoryMock.Setup(r => r.GetMetadataCountAsync(It.IsAny<ListComplianceMetadataRequest>()))
                .ReturnsAsync(1);
            _repositoryMock.Setup(r => r.GetAuditLogAsync(It.IsAny<GetComplianceAuditLogRequest>()))
                .ReturnsAsync(new List<ComplianceAuditLogEntry>());

            // Act
            var result = await _service.GetComplianceReportAsync(request, requestedBy);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Tokens[0].ComplianceHealthScore, Is.GreaterThan(0));
            Assert.That(result.Tokens[0].ComplianceHealthScore, Is.LessThanOrEqualTo(100));
            // With all fields filled, should have high score
            Assert.That(result.Tokens[0].ComplianceHealthScore, Is.EqualTo(100));
        }

        [Test]
        public async Task GetComplianceReportAsync_VOINetwork_ShouldEvaluateNetworkCompliance()
        {
            // Arrange
            var request = new GetTokenComplianceReportRequest
            {
                Network = "voimain-v1.0",
                Page = 1,
                PageSize = 10
            };
            var requestedBy = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";

            var metadataList = new List<ComplianceMetadata>
            {
                new ComplianceMetadata
                {
                    AssetId = 12345,
                    Network = "voimain-v1.0",
                    ComplianceStatus = ComplianceStatus.Compliant,
                    VerificationStatus = VerificationStatus.Verified,
                    RequiresAccreditedInvestors = true,
                    Jurisdiction = "US"
                }
            };

            _repositoryMock.Setup(r => r.ListMetadataAsync(It.IsAny<ListComplianceMetadataRequest>()))
                .ReturnsAsync(metadataList);
            _repositoryMock.Setup(r => r.GetMetadataCountAsync(It.IsAny<ListComplianceMetadataRequest>()))
                .ReturnsAsync(1);
            _repositoryMock.Setup(r => r.GetAuditLogAsync(It.IsAny<GetComplianceAuditLogRequest>()))
                .ReturnsAsync(new List<ComplianceAuditLogEntry>());

            // Act
            var result = await _service.GetComplianceReportAsync(request, requestedBy);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Tokens[0].NetworkSpecificStatus, Is.Not.Null);
            Assert.That(result.Tokens[0].NetworkSpecificStatus!.MeetsNetworkRequirements, Is.True);
            Assert.That(result.Tokens[0].NetworkSpecificStatus.SatisfiedRules.Count, Is.GreaterThan(0));
        }

        [Test]
        public async Task GetComplianceReportAsync_VOINetworkMissingJurisdiction_ShouldFlagViolation()
        {
            // Arrange
            var request = new GetTokenComplianceReportRequest
            {
                Network = "voimain-v1.0",
                Page = 1,
                PageSize = 10
            };
            var requestedBy = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";

            var metadataList = new List<ComplianceMetadata>
            {
                new ComplianceMetadata
                {
                    AssetId = 12345,
                    Network = "voimain-v1.0",
                    ComplianceStatus = ComplianceStatus.Compliant,
                    VerificationStatus = VerificationStatus.Verified,
                    RequiresAccreditedInvestors = true,
                    Jurisdiction = null // Missing jurisdiction
                }
            };

            _repositoryMock.Setup(r => r.ListMetadataAsync(It.IsAny<ListComplianceMetadataRequest>()))
                .ReturnsAsync(metadataList);
            _repositoryMock.Setup(r => r.GetMetadataCountAsync(It.IsAny<ListComplianceMetadataRequest>()))
                .ReturnsAsync(1);
            _repositoryMock.Setup(r => r.GetAuditLogAsync(It.IsAny<GetComplianceAuditLogRequest>()))
                .ReturnsAsync(new List<ComplianceAuditLogEntry>());

            // Act
            var result = await _service.GetComplianceReportAsync(request, requestedBy);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Tokens[0].NetworkSpecificStatus, Is.Not.Null);
            Assert.That(result.Tokens[0].NetworkSpecificStatus!.MeetsNetworkRequirements, Is.False);
            Assert.That(result.Tokens[0].NetworkSpecificStatus.ViolatedRules.Count, Is.GreaterThan(0));
            Assert.That(result.Tokens[0].NetworkSpecificStatus.Recommendations.Count, Is.GreaterThan(0));
        }

        [Test]
        public async Task GetComplianceReportAsync_AramidNetwork_ShouldEvaluateRegulatoryFramework()
        {
            // Arrange
            var request = new GetTokenComplianceReportRequest
            {
                Network = "aramidmain-v1.0",
                Page = 1,
                PageSize = 10
            };
            var requestedBy = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";

            var metadataList = new List<ComplianceMetadata>
            {
                new ComplianceMetadata
                {
                    AssetId = 12345,
                    Network = "aramidmain-v1.0",
                    ComplianceStatus = ComplianceStatus.Compliant,
                    RegulatoryFramework = "MICA",
                    AssetType = "Security Token",
                    MaxHolders = 500
                }
            };

            _repositoryMock.Setup(r => r.ListMetadataAsync(It.IsAny<ListComplianceMetadataRequest>()))
                .ReturnsAsync(metadataList);
            _repositoryMock.Setup(r => r.GetMetadataCountAsync(It.IsAny<ListComplianceMetadataRequest>()))
                .ReturnsAsync(1);
            _repositoryMock.Setup(r => r.GetAuditLogAsync(It.IsAny<GetComplianceAuditLogRequest>()))
                .ReturnsAsync(new List<ComplianceAuditLogEntry>());

            // Act
            var result = await _service.GetComplianceReportAsync(request, requestedBy);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Tokens[0].NetworkSpecificStatus, Is.Not.Null);
            Assert.That(result.Tokens[0].NetworkSpecificStatus!.MeetsNetworkRequirements, Is.True);
            Assert.That(result.Tokens[0].NetworkSpecificStatus.SatisfiedRules.Count, Is.GreaterThan(0));
        }

        [Test]
        public async Task GetComplianceReportAsync_AramidNetworkMissingMaxHolders_ShouldFlagViolation()
        {
            // Arrange
            var request = new GetTokenComplianceReportRequest
            {
                Network = "aramidmain-v1.0",
                Page = 1,
                PageSize = 10
            };
            var requestedBy = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";

            var metadataList = new List<ComplianceMetadata>
            {
                new ComplianceMetadata
                {
                    AssetId = 12345,
                    Network = "aramidmain-v1.0",
                    ComplianceStatus = ComplianceStatus.Compliant,
                    RegulatoryFramework = "MICA",
                    AssetType = "Security Token",
                    MaxHolders = null // Missing MaxHolders for security token
                }
            };

            _repositoryMock.Setup(r => r.ListMetadataAsync(It.IsAny<ListComplianceMetadataRequest>()))
                .ReturnsAsync(metadataList);
            _repositoryMock.Setup(r => r.GetMetadataCountAsync(It.IsAny<ListComplianceMetadataRequest>()))
                .ReturnsAsync(1);
            _repositoryMock.Setup(r => r.GetAuditLogAsync(It.IsAny<GetComplianceAuditLogRequest>()))
                .ReturnsAsync(new List<ComplianceAuditLogEntry>());

            // Act
            var result = await _service.GetComplianceReportAsync(request, requestedBy);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Tokens[0].NetworkSpecificStatus, Is.Not.Null);
            Assert.That(result.Tokens[0].NetworkSpecificStatus!.MeetsNetworkRequirements, Is.False);
            Assert.That(result.Tokens[0].NetworkSpecificStatus.ViolatedRules.Count, Is.GreaterThan(0));
        }

        [Test]
        public async Task GetComplianceReportAsync_ExpiredVerification_ShouldGenerateWarning()
        {
            // Arrange
            var request = new GetTokenComplianceReportRequest
            {
                Network = "voimain-v1.0",
                Page = 1,
                PageSize = 10
            };
            var requestedBy = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";

            var metadataList = new List<ComplianceMetadata>
            {
                new ComplianceMetadata
                {
                    AssetId = 12345,
                    Network = "voimain-v1.0",
                    ComplianceStatus = ComplianceStatus.Compliant,
                    VerificationStatus = VerificationStatus.Expired
                }
            };

            _repositoryMock.Setup(r => r.ListMetadataAsync(It.IsAny<ListComplianceMetadataRequest>()))
                .ReturnsAsync(metadataList);
            _repositoryMock.Setup(r => r.GetMetadataCountAsync(It.IsAny<ListComplianceMetadataRequest>()))
                .ReturnsAsync(1);
            _repositoryMock.Setup(r => r.GetAuditLogAsync(It.IsAny<GetComplianceAuditLogRequest>()))
                .ReturnsAsync(new List<ComplianceAuditLogEntry>());

            // Act
            var result = await _service.GetComplianceReportAsync(request, requestedBy);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Tokens[0].Warnings, Is.Not.Empty);
            Assert.That(result.Tokens[0].Warnings.Any(w => w.Contains("expired")), Is.True);
        }

        [Test]
        public async Task GetComplianceReportAsync_OverdueReview_ShouldGenerateWarning()
        {
            // Arrange
            var request = new GetTokenComplianceReportRequest
            {
                Network = "voimain-v1.0",
                Page = 1,
                PageSize = 10
            };
            var requestedBy = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";

            var metadataList = new List<ComplianceMetadata>
            {
                new ComplianceMetadata
                {
                    AssetId = 12345,
                    Network = "voimain-v1.0",
                    ComplianceStatus = ComplianceStatus.Compliant,
                    VerificationStatus = VerificationStatus.Verified,
                    NextComplianceReview = DateTime.UtcNow.AddDays(-1) // Overdue
                }
            };

            _repositoryMock.Setup(r => r.ListMetadataAsync(It.IsAny<ListComplianceMetadataRequest>()))
                .ReturnsAsync(metadataList);
            _repositoryMock.Setup(r => r.GetMetadataCountAsync(It.IsAny<ListComplianceMetadataRequest>()))
                .ReturnsAsync(1);
            _repositoryMock.Setup(r => r.GetAuditLogAsync(It.IsAny<GetComplianceAuditLogRequest>()))
                .ReturnsAsync(new List<ComplianceAuditLogEntry>());

            // Act
            var result = await _service.GetComplianceReportAsync(request, requestedBy);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Tokens[0].Warnings, Is.Not.Empty);
            Assert.That(result.Tokens[0].Warnings.Any(w => w.Contains("overdue")), Is.True);
        }

        [Test]
        public async Task GetComplianceReportAsync_ShouldEmitMeteringEvent()
        {
            // Arrange
            var request = new GetTokenComplianceReportRequest
            {
                Network = "voimain-v1.0",
                Page = 1,
                PageSize = 10
            };
            var requestedBy = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";

            var metadataList = new List<ComplianceMetadata>
            {
                new ComplianceMetadata
                {
                    AssetId = 12345,
                    Network = "voimain-v1.0",
                    ComplianceStatus = ComplianceStatus.Compliant
                }
            };

            _repositoryMock.Setup(r => r.ListMetadataAsync(It.IsAny<ListComplianceMetadataRequest>()))
                .ReturnsAsync(metadataList);
            _repositoryMock.Setup(r => r.GetMetadataCountAsync(It.IsAny<ListComplianceMetadataRequest>()))
                .ReturnsAsync(1);
            _repositoryMock.Setup(r => r.GetAuditLogAsync(It.IsAny<GetComplianceAuditLogRequest>()))
                .ReturnsAsync(new List<ComplianceAuditLogEntry>());

            // Act
            var result = await _service.GetComplianceReportAsync(request, requestedBy);

            // Assert
            Assert.That(result.Success, Is.True);
            _meteringServiceMock.Verify(
                m => m.EmitMeteringEvent(It.Is<SubscriptionMeteringEvent>(
                    e => e.Category == MeteringCategory.Compliance &&
                         e.PerformedBy == requestedBy &&
                         e.ItemCount == 1)),
                Times.Once);
        }

        [Test]
        public async Task GetComplianceReportAsync_EmptyResult_ShouldSucceed()
        {
            // Arrange
            var request = new GetTokenComplianceReportRequest
            {
                Network = "voimain-v1.0",
                Page = 1,
                PageSize = 10
            };
            var requestedBy = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";

            _repositoryMock.Setup(r => r.ListMetadataAsync(It.IsAny<ListComplianceMetadataRequest>()))
                .ReturnsAsync(new List<ComplianceMetadata>());
            _repositoryMock.Setup(r => r.GetMetadataCountAsync(It.IsAny<ListComplianceMetadataRequest>()))
                .ReturnsAsync(0);

            // Act
            var result = await _service.GetComplianceReportAsync(request, requestedBy);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Tokens, Is.Empty);
            Assert.That(result.TotalCount, Is.EqualTo(0));
        }

        #endregion
    }
}

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
    public class ComplianceServiceTests
    {
        private Mock<IComplianceRepository> _repositoryMock;
        private Mock<IWhitelistService> _whitelistServiceMock;
        private Mock<ILogger<ComplianceService>> _loggerMock;
        private Mock<ISubscriptionMeteringService> _meteringServiceMock;
        private ComplianceService _service;

        [SetUp]
        public void Setup()
        {
            _repositoryMock = new Mock<IComplianceRepository>();
            _whitelistServiceMock = new Mock<IWhitelistService>();
            _loggerMock = new Mock<ILogger<ComplianceService>>();
            _meteringServiceMock = new Mock<ISubscriptionMeteringService>();
            _service = new ComplianceService(_repositoryMock.Object, _whitelistServiceMock.Object, _loggerMock.Object, _meteringServiceMock.Object, Mock.Of<IWebhookService>());
        }

        #region UpsertMetadataAsync Tests

        [Test]
        public async Task UpsertMetadataAsync_ValidRequest_ShouldSucceed()
        {
            // Arrange
            var request = new UpsertComplianceMetadataRequest
            {
                AssetId = 12345,
                KycProvider = "Sumsub",
                KycVerificationDate = DateTime.UtcNow,
                VerificationStatus = VerificationStatus.Verified,
                Jurisdiction = "US,EU",
                RegulatoryFramework = "SEC Reg D",
                ComplianceStatus = ComplianceStatus.Compliant
            };
            var createdBy = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";

            _repositoryMock.Setup(r => r.GetMetadataByAssetIdAsync(request.AssetId))
                .ReturnsAsync((ComplianceMetadata?)null);
            _repositoryMock.Setup(r => r.UpsertMetadataAsync(It.IsAny<ComplianceMetadata>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.UpsertMetadataAsync(request, createdBy);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Metadata, Is.Not.Null);
            Assert.That(result.Metadata!.AssetId, Is.EqualTo(request.AssetId));
            Assert.That(result.Metadata.CreatedBy, Is.EqualTo(createdBy));
            Assert.That(result.Metadata.KycProvider, Is.EqualTo(request.KycProvider));
        }

        [Test]
        public async Task UpsertMetadataAsync_UpdateExisting_ShouldPreserveCreationInfo()
        {
            // Arrange
            var originalCreatedBy = "CREATOR1AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";
            var originalCreatedAt = DateTime.UtcNow.AddDays(-10);
            var existingMetadata = new ComplianceMetadata
            {
                AssetId = 12345,
                CreatedBy = originalCreatedBy,
                CreatedAt = originalCreatedAt,
                ComplianceStatus = ComplianceStatus.UnderReview
            };

            var request = new UpsertComplianceMetadataRequest
            {
                AssetId = 12345,
                ComplianceStatus = ComplianceStatus.Compliant,
                RegulatoryFramework = "SEC Reg D"
            };
            var updatedBy = "UPDATER1AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";

            _repositoryMock.Setup(r => r.GetMetadataByAssetIdAsync(request.AssetId))
                .ReturnsAsync(existingMetadata);
            _repositoryMock.Setup(r => r.UpsertMetadataAsync(It.IsAny<ComplianceMetadata>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.UpsertMetadataAsync(request, updatedBy);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Metadata!.CreatedBy, Is.EqualTo(originalCreatedBy));
            Assert.That(result.Metadata.CreatedAt, Is.EqualTo(originalCreatedAt));
            Assert.That(result.Metadata.UpdatedBy, Is.EqualTo(updatedBy));
            Assert.That(result.Metadata.UpdatedAt, Is.Not.Null);
        }

        #endregion

        #region GetMetadataAsync Tests

        [Test]
        public async Task GetMetadataAsync_ExistingMetadata_ShouldReturnSuccess()
        {
            // Arrange
            var assetId = 12345ul;
            var metadata = new ComplianceMetadata
            {
                AssetId = assetId,
                ComplianceStatus = ComplianceStatus.Compliant
            };

            _repositoryMock.Setup(r => r.GetMetadataByAssetIdAsync(assetId))
                .ReturnsAsync(metadata);

            // Act
            var result = await _service.GetMetadataAsync(assetId);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Metadata, Is.Not.Null);
            Assert.That(result.Metadata!.AssetId, Is.EqualTo(assetId));
        }

        [Test]
        public async Task GetMetadataAsync_NonExistingMetadata_ShouldReturnFailure()
        {
            // Arrange
            var assetId = 99999ul;
            _repositoryMock.Setup(r => r.GetMetadataByAssetIdAsync(assetId))
                .ReturnsAsync((ComplianceMetadata?)null);

            // Act
            var result = await _service.GetMetadataAsync(assetId);

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("not found"));
        }

        #endregion

        #region DeleteMetadataAsync Tests

        [Test]
        public async Task DeleteMetadataAsync_ExistingMetadata_ShouldSucceed()
        {
            // Arrange
            var assetId = 12345ul;
            _repositoryMock.Setup(r => r.DeleteMetadataAsync(assetId))
                .ReturnsAsync(true);

            // Act
            var result = await _service.DeleteMetadataAsync(assetId);

            // Assert
            Assert.That(result.Success, Is.True);
        }

        [Test]
        public async Task DeleteMetadataAsync_NonExistingMetadata_ShouldReturnFailure()
        {
            // Arrange
            var assetId = 99999ul;
            _repositoryMock.Setup(r => r.DeleteMetadataAsync(assetId))
                .ReturnsAsync(false);

            // Act
            var result = await _service.DeleteMetadataAsync(assetId);

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("not found"));
        }

        #endregion

        #region ListMetadataAsync Tests

        [Test]
        public async Task ListMetadataAsync_WithoutFilters_ShouldReturnAllMetadata()
        {
            // Arrange
            var request = new ListComplianceMetadataRequest
            {
                Page = 1,
                PageSize = 20
            };

            var metadataList = new List<ComplianceMetadata>
            {
                new ComplianceMetadata { AssetId = 1 },
                new ComplianceMetadata { AssetId = 2 },
                new ComplianceMetadata { AssetId = 3 }
            };

            _repositoryMock.Setup(r => r.ListMetadataAsync(request))
                .ReturnsAsync(metadataList);
            _repositoryMock.Setup(r => r.GetMetadataCountAsync(request))
                .ReturnsAsync(metadataList.Count);

            // Act
            var result = await _service.ListMetadataAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Metadata.Count, Is.EqualTo(3));
            Assert.That(result.TotalCount, Is.EqualTo(3));
            Assert.That(result.TotalPages, Is.EqualTo(1));
        }

        [Test]
        public async Task ListMetadataAsync_WithPagination_ShouldCalculateCorrectPages()
        {
            // Arrange
            var request = new ListComplianceMetadataRequest
            {
                Page = 1,
                PageSize = 2
            };

            var metadataList = new List<ComplianceMetadata>
            {
                new ComplianceMetadata { AssetId = 1 },
                new ComplianceMetadata { AssetId = 2 }
            };

            _repositoryMock.Setup(r => r.ListMetadataAsync(request))
                .ReturnsAsync(metadataList);
            _repositoryMock.Setup(r => r.GetMetadataCountAsync(request))
                .ReturnsAsync(5); // Total 5 items

            // Act
            var result = await _service.ListMetadataAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.TotalCount, Is.EqualTo(5));
            Assert.That(result.TotalPages, Is.EqualTo(3)); // 5 items / 2 per page = 3 pages
        }

        #endregion

        #region Network Validation Tests - VOI Network

        [Test]
        public void ValidateNetworkRules_VoiWithAccreditedInvestorsNotVerified_ShouldReturnError()
        {
            // Arrange
            var network = "voimain-v1.0";
            var request = new UpsertComplianceMetadataRequest
            {
                AssetId = 12345,
                Network = network,
                RequiresAccreditedInvestors = true,
                VerificationStatus = VerificationStatus.Pending,
                Jurisdiction = "US"
            };

            // Act
            var error = _service.ValidateNetworkRules(network, request);

            // Assert
            Assert.That(error, Is.Not.Null);
            Assert.That(error, Does.Contain("VOI").And.Contains("KYC verification"));
        }

        [Test]
        public void ValidateNetworkRules_VoiWithVerifiedAccreditedInvestors_ShouldPass()
        {
            // Arrange
            var network = "voimain-v1.0";
            var request = new UpsertComplianceMetadataRequest
            {
                AssetId = 12345,
                Network = network,
                RequiresAccreditedInvestors = true,
                VerificationStatus = VerificationStatus.Verified,
                Jurisdiction = "US"
            };

            // Act
            var error = _service.ValidateNetworkRules(network, request);

            // Assert
            Assert.That(error, Is.Null);
        }

        [Test]
        public void ValidateNetworkRules_VoiWithoutJurisdiction_ShouldReturnError()
        {
            // Arrange
            var network = "voimain-v1.0";
            var request = new UpsertComplianceMetadataRequest
            {
                AssetId = 12345,
                Network = network,
                Jurisdiction = null
            };

            // Act
            var error = _service.ValidateNetworkRules(network, request);

            // Assert
            Assert.That(error, Is.Not.Null);
            Assert.That(error, Does.Contain("VOI").And.Contains("jurisdiction"));
        }

        [Test]
        public void ValidateNetworkRules_VoiWithJurisdiction_ShouldPass()
        {
            // Arrange
            var network = "voimain-v1.0";
            var request = new UpsertComplianceMetadataRequest
            {
                AssetId = 12345,
                Network = network,
                Jurisdiction = "US",
                VerificationStatus = VerificationStatus.Pending,
                RequiresAccreditedInvestors = false
            };

            // Act
            var error = _service.ValidateNetworkRules(network, request);

            // Assert
            Assert.That(error, Is.Null);
        }

        #endregion

        #region Network Validation Tests - Aramid Network

        [Test]
        public void ValidateNetworkRules_AramidCompliantWithoutFramework_ShouldReturnError()
        {
            // Arrange
            var network = "aramidmain-v1.0";
            var request = new UpsertComplianceMetadataRequest
            {
                AssetId = 12345,
                Network = network,
                ComplianceStatus = ComplianceStatus.Compliant,
                RegulatoryFramework = null
            };

            // Act
            var error = _service.ValidateNetworkRules(network, request);

            // Assert
            Assert.That(error, Is.Not.Null);
            Assert.That(error, Does.Contain("Aramid").And.Contains("RegulatoryFramework"));
        }

        [Test]
        public void ValidateNetworkRules_AramidCompliantWithFramework_ShouldPass()
        {
            // Arrange
            var network = "aramidmain-v1.0";
            var request = new UpsertComplianceMetadataRequest
            {
                AssetId = 12345,
                Network = network,
                ComplianceStatus = ComplianceStatus.Compliant,
                RegulatoryFramework = "MiFID II"
            };

            // Act
            var error = _service.ValidateNetworkRules(network, request);

            // Assert
            Assert.That(error, Is.Null);
        }

        [Test]
        public void ValidateNetworkRules_AramidSecurityTokenWithoutMaxHolders_ShouldReturnError()
        {
            // Arrange
            var network = "aramidmain-v1.0";
            var request = new UpsertComplianceMetadataRequest
            {
                AssetId = 12345,
                Network = network,
                AssetType = "Security Token",
                MaxHolders = null
            };

            // Act
            var error = _service.ValidateNetworkRules(network, request);

            // Assert
            Assert.That(error, Is.Not.Null);
            Assert.That(error, Does.Contain("Aramid").And.Contains("MaxHolders"));
        }

        [Test]
        public void ValidateNetworkRules_AramidSecurityTokenWithMaxHolders_ShouldPass()
        {
            // Arrange
            var network = "aramidmain-v1.0";
            var request = new UpsertComplianceMetadataRequest
            {
                AssetId = 12345,
                Network = network,
                AssetType = "Security Token",
                MaxHolders = 500,
                ComplianceStatus = ComplianceStatus.UnderReview
            };

            // Act
            var error = _service.ValidateNetworkRules(network, request);

            // Assert
            Assert.That(error, Is.Null);
        }

        #endregion

        #region Network Validation Tests - General

        [Test]
        public void ValidateNetworkRules_NoNetworkSpecified_ShouldPass()
        {
            // Arrange
            var request = new UpsertComplianceMetadataRequest
            {
                AssetId = 12345,
                Network = null
            };

            // Act
            var error = _service.ValidateNetworkRules(null, request);

            // Assert
            Assert.That(error, Is.Null);
        }

        [Test]
        public void ValidateNetworkRules_EmptyNetwork_ShouldPass()
        {
            // Arrange
            var request = new UpsertComplianceMetadataRequest
            {
                AssetId = 12345,
                Network = ""
            };

            // Act
            var error = _service.ValidateNetworkRules("", request);

            // Assert
            Assert.That(error, Is.Null);
        }

        [Test]
        public void ValidateNetworkRules_OtherNetwork_ShouldPass()
        {
            // Arrange
            var network = "mainnet-v1.0";
            var request = new UpsertComplianceMetadataRequest
            {
                AssetId = 12345,
                Network = network
            };

            // Act
            var error = _service.ValidateNetworkRules(network, request);

            // Assert
            Assert.That(error, Is.Null);
        }

        #endregion

        #region Network Validation with Upsert

        [Test]
        public async Task UpsertMetadataAsync_VoiNetworkViolation_ShouldReturnError()
        {
            // Arrange
            var request = new UpsertComplianceMetadataRequest
            {
                AssetId = 12345,
                Network = "voimain-v1.0",
                RequiresAccreditedInvestors = true,
                VerificationStatus = VerificationStatus.Pending
            };
            var createdBy = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";

            // Act
            var result = await _service.UpsertMetadataAsync(request, createdBy);

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("VOI"));
        }

        [Test]
        public async Task UpsertMetadataAsync_AramidNetworkViolation_ShouldReturnError()
        {
            // Arrange
            var request = new UpsertComplianceMetadataRequest
            {
                AssetId = 12345,
                Network = "aramidmain-v1.0",
                ComplianceStatus = ComplianceStatus.Compliant
                // Missing RegulatoryFramework
            };
            var createdBy = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";

            // Act
            var result = await _service.UpsertMetadataAsync(request, createdBy);

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("Aramid"));
        }

        #endregion

        #region Metering Tests

        [Test]
        public async Task UpsertMetadataAsync_Success_ShouldEmitMeteringEvent()
        {
            // Arrange
            var request = new UpsertComplianceMetadataRequest
            {
                AssetId = 12345,
                Network = "voimain",
                KycProvider = "Sumsub",
                VerificationStatus = VerificationStatus.Verified,
                Jurisdiction = "US"
            };
            var createdBy = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";

            _repositoryMock.Setup(r => r.GetMetadataByAssetIdAsync(request.AssetId))
                .ReturnsAsync((ComplianceMetadata?)null);
            _repositoryMock.Setup(r => r.UpsertMetadataAsync(It.IsAny<ComplianceMetadata>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.UpsertMetadataAsync(request, createdBy);

            // Assert
            Assert.That(result.Success, Is.True);
            _meteringServiceMock.Verify(
                m => m.EmitMeteringEvent(It.Is<SubscriptionMeteringEvent>(
                    e => e.Category == MeteringCategory.Compliance &&
                         e.OperationType == MeteringOperationType.Upsert &&
                         e.AssetId == request.AssetId &&
                         e.Network == request.Network &&
                         e.PerformedBy == createdBy &&
                         e.ItemCount == 1
                )),
                Times.Once);
        }

        [Test]
        public async Task UpsertMetadataAsync_Failure_ShouldNotEmitMeteringEvent()
        {
            // Arrange
            var request = new UpsertComplianceMetadataRequest
            {
                AssetId = 12345,
                Network = "voimain",
                Jurisdiction = "US"
            };
            var createdBy = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";

            _repositoryMock.Setup(r => r.GetMetadataByAssetIdAsync(request.AssetId))
                .ReturnsAsync((ComplianceMetadata?)null);
            _repositoryMock.Setup(r => r.UpsertMetadataAsync(It.IsAny<ComplianceMetadata>()))
                .ReturnsAsync(false);

            // Act
            var result = await _service.UpsertMetadataAsync(request, createdBy);

            // Assert
            Assert.That(result.Success, Is.False);
            _meteringServiceMock.Verify(
                m => m.EmitMeteringEvent(It.IsAny<SubscriptionMeteringEvent>()),
                Times.Never);
        }

        [Test]
        public async Task DeleteMetadataAsync_Success_ShouldEmitMeteringEvent()
        {
            // Arrange
            var assetId = 12345ul;

            _repositoryMock.Setup(r => r.DeleteMetadataAsync(assetId))
                .ReturnsAsync(true);

            // Act
            var result = await _service.DeleteMetadataAsync(assetId);

            // Assert
            Assert.That(result.Success, Is.True);
            _meteringServiceMock.Verify(
                m => m.EmitMeteringEvent(It.Is<SubscriptionMeteringEvent>(
                    e => e.Category == MeteringCategory.Compliance &&
                         e.OperationType == MeteringOperationType.Delete &&
                         e.AssetId == assetId &&
                         e.ItemCount == 1
                )),
                Times.Once);
        }

        [Test]
        public async Task DeleteMetadataAsync_Failure_ShouldNotEmitMeteringEvent()
        {
            // Arrange
            var assetId = 12345ul;

            _repositoryMock.Setup(r => r.DeleteMetadataAsync(assetId))
                .ReturnsAsync(false);

            // Act
            var result = await _service.DeleteMetadataAsync(assetId);

            // Assert
            Assert.That(result.Success, Is.False);
            _meteringServiceMock.Verify(
                m => m.EmitMeteringEvent(It.IsAny<SubscriptionMeteringEvent>()),
                Times.Never);
        }

        #endregion

        #region GenerateComplianceEvidenceBundleAsync Tests

        [Test]
        public async Task GenerateComplianceEvidenceBundleAsync_WithAllData_ShouldSucceed()
        {
            // Arrange
            var assetId = 12345ul;
            var requestedBy = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";
            var request = new GenerateComplianceEvidenceBundleRequest
            {
                AssetId = assetId,
                FromDate = DateTime.UtcNow.AddDays(-30),
                ToDate = DateTime.UtcNow,
                IncludeWhitelistHistory = true,
                IncludeTransferApprovals = true,
                IncludeAuditLogs = true,
                IncludePolicyMetadata = true,
                IncludeTokenMetadata = true
            };

            // Mock compliance metadata
            var metadata = new ComplianceMetadata
            {
                AssetId = assetId,
                Network = "voimain-v1.0",
                ComplianceStatus = ComplianceStatus.Compliant,
                KycProvider = "Sumsub",
                Jurisdiction = "US,EU"
            };
            _repositoryMock.Setup(r => r.GetMetadataByAssetIdAsync(assetId))
                .ReturnsAsync(metadata);

            // Mock whitelist entries
            _whitelistServiceMock.Setup(w => w.ListEntriesAsync(It.IsAny<BiatecTokensApi.Models.Whitelist.ListWhitelistRequest>()))
                .ReturnsAsync(new BiatecTokensApi.Models.Whitelist.WhitelistListResponse
                {
                    Success = true,
                    Entries = new List<BiatecTokensApi.Models.Whitelist.WhitelistEntry>
                    {
                        new BiatecTokensApi.Models.Whitelist.WhitelistEntry
                        {
                            AssetId = assetId,
                            Address = "TESTADDR1",
                            CreatedAt = DateTime.UtcNow.AddDays(-10)
                        }
                    }
                });

            // Mock whitelist audit log
            _whitelistServiceMock.Setup(w => w.GetAuditLogAsync(It.IsAny<BiatecTokensApi.Models.Whitelist.GetWhitelistAuditLogRequest>()))
                .ReturnsAsync(new BiatecTokensApi.Models.Whitelist.WhitelistAuditLogResponse
                {
                    Success = true,
                    Entries = new List<BiatecTokensApi.Models.Whitelist.WhitelistAuditLogEntry>
                    {
                        new BiatecTokensApi.Models.Whitelist.WhitelistAuditLogEntry
                        {
                            AssetId = assetId,
                            ActionType = BiatecTokensApi.Models.Whitelist.WhitelistActionType.Add,
                            PerformedBy = requestedBy,
                            PerformedAt = DateTime.UtcNow.AddDays(-5)
                        }
                    }
                });

            // Mock compliance audit log
            _repositoryMock.Setup(r => r.GetAuditLogAsync(It.IsAny<GetComplianceAuditLogRequest>()))
                .ReturnsAsync(new List<ComplianceAuditLogEntry>
                {
                    new ComplianceAuditLogEntry
                    {
                        AssetId = assetId,
                        ActionType = ComplianceActionType.Create,
                        PerformedBy = requestedBy,
                        Success = true,
                        PerformedAt = DateTime.UtcNow.AddDays(-2)
                    }
                });

            _repositoryMock.Setup(r => r.GetAuditLogCountAsync(It.IsAny<GetComplianceAuditLogRequest>()))
                .ReturnsAsync(1);

            _repositoryMock.Setup(r => r.AddAuditLogEntryAsync(It.IsAny<ComplianceAuditLogEntry>()))
                .Returns(Task.FromResult(true));

            _meteringServiceMock.Setup(m => m.EmitMeteringEvent(It.IsAny<SubscriptionMeteringEvent>()));

            // Act
            var result = await _service.GenerateComplianceEvidenceBundleAsync(request, requestedBy);

            // Assert
            Assert.That(result.Success, Is.True, $"Bundle generation failed: {result.ErrorMessage}");
            Assert.That(result.BundleMetadata, Is.Not.Null);
            Assert.That(result.BundleMetadata!.AssetId, Is.EqualTo(assetId));
            Assert.That(result.BundleMetadata.GeneratedBy, Is.EqualTo(requestedBy));
            Assert.That(result.BundleMetadata.BundleId, Is.Not.Empty);
            Assert.That(result.BundleMetadata.BundleSha256, Is.Not.Empty);
            Assert.That(result.BundleMetadata.Files, Is.Not.Empty);
            Assert.That(result.ZipContent, Is.Not.Null);
            Assert.That(result.ZipContent!.Length, Is.GreaterThan(0));
            Assert.That(result.FileName, Is.Not.Null);
            Assert.That(result.FileName!.Contains($"{assetId}"), Is.True);
            Assert.That(result.FileName.EndsWith(".zip"), Is.True);

            // Verify metering event was emitted
            _meteringServiceMock.Verify(
                m => m.EmitMeteringEvent(It.Is<SubscriptionMeteringEvent>(
                    e => e.OperationType == MeteringOperationType.Export &&
                         e.PerformedBy == requestedBy &&
                         e.AssetId == assetId
                )),
                Times.Once);

            // Verify audit log entry was created
            _repositoryMock.Verify(
                r => r.AddAuditLogEntryAsync(It.Is<ComplianceAuditLogEntry>(
                    e => e.AssetId == assetId &&
                         e.ActionType == ComplianceActionType.Export &&
                         e.PerformedBy == requestedBy &&
                         e.Success == true
                )),
                Times.Once);
        }

        [Test]
        public async Task GenerateComplianceEvidenceBundleAsync_WithDateFilter_ShouldApplyFilters()
        {
            // Arrange
            var assetId = 12345ul;
            var requestedBy = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";
            var fromDate = DateTime.UtcNow.AddDays(-7);
            var toDate = DateTime.UtcNow;
            var request = new GenerateComplianceEvidenceBundleRequest
            {
                AssetId = assetId,
                FromDate = fromDate,
                ToDate = toDate
            };

            _repositoryMock.Setup(r => r.GetMetadataByAssetIdAsync(assetId))
                .ReturnsAsync(new ComplianceMetadata { AssetId = assetId });

            _whitelistServiceMock.Setup(w => w.ListEntriesAsync(It.IsAny<BiatecTokensApi.Models.Whitelist.ListWhitelistRequest>()))
                .ReturnsAsync(new BiatecTokensApi.Models.Whitelist.WhitelistListResponse { Success = true, Entries = new List<BiatecTokensApi.Models.Whitelist.WhitelistEntry>() });

            _whitelistServiceMock.Setup(w => w.GetAuditLogAsync(It.IsAny<BiatecTokensApi.Models.Whitelist.GetWhitelistAuditLogRequest>()))
                .ReturnsAsync(new BiatecTokensApi.Models.Whitelist.WhitelistAuditLogResponse { Success = true, Entries = new List<BiatecTokensApi.Models.Whitelist.WhitelistAuditLogEntry>() });

            _repositoryMock.Setup(r => r.GetAuditLogAsync(It.IsAny<GetComplianceAuditLogRequest>()))
                .ReturnsAsync(new List<ComplianceAuditLogEntry>());

            _repositoryMock.Setup(r => r.GetAuditLogCountAsync(It.IsAny<GetComplianceAuditLogRequest>()))
                .ReturnsAsync(0);

            _repositoryMock.Setup(r => r.AddAuditLogEntryAsync(It.IsAny<ComplianceAuditLogEntry>()))
                .Returns(Task.FromResult(true));

            _meteringServiceMock.Setup(m => m.EmitMeteringEvent(It.IsAny<SubscriptionMeteringEvent>()));

            // Act
            var result = await _service.GenerateComplianceEvidenceBundleAsync(request, requestedBy);

            // Assert
            Assert.That(result.Success, Is.True, $"Bundle generation failed: {result.ErrorMessage}");
            Assert.That(result.BundleMetadata, Is.Not.Null);
            Assert.That(result.BundleMetadata!.FromDate, Is.EqualTo(fromDate));
            Assert.That(result.BundleMetadata.ToDate, Is.EqualTo(toDate));

            // Verify date filters were passed to audit log requests
            _whitelistServiceMock.Verify(
                w => w.GetAuditLogAsync(It.Is<BiatecTokensApi.Models.Whitelist.GetWhitelistAuditLogRequest>(
                    req => req.FromDate == fromDate && req.ToDate == toDate
                )),
                Times.AtLeastOnce);

            _repositoryMock.Verify(
                r => r.GetAuditLogAsync(It.Is<GetComplianceAuditLogRequest>(
                    req => req.FromDate == fromDate && req.ToDate == toDate
                )),
                Times.AtLeastOnce);
        }

        [Test]
        public async Task GenerateComplianceEvidenceBundleAsync_BundleContainsManifest_ShouldIncludeChecksums()
        {
            // Arrange
            var assetId = 12345ul;
            var requestedBy = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";
            var request = new GenerateComplianceEvidenceBundleRequest { AssetId = assetId };

            _repositoryMock.Setup(r => r.GetMetadataByAssetIdAsync(assetId))
                .ReturnsAsync(new ComplianceMetadata { AssetId = assetId });

            _whitelistServiceMock.Setup(w => w.ListEntriesAsync(It.IsAny<BiatecTokensApi.Models.Whitelist.ListWhitelistRequest>()))
                .ReturnsAsync(new BiatecTokensApi.Models.Whitelist.WhitelistListResponse { Success = true, Entries = new List<BiatecTokensApi.Models.Whitelist.WhitelistEntry>() });

            _whitelistServiceMock.Setup(w => w.GetAuditLogAsync(It.IsAny<BiatecTokensApi.Models.Whitelist.GetWhitelistAuditLogRequest>()))
                .ReturnsAsync(new BiatecTokensApi.Models.Whitelist.WhitelistAuditLogResponse { Success = true, Entries = new List<BiatecTokensApi.Models.Whitelist.WhitelistAuditLogEntry>() });

            _repositoryMock.Setup(r => r.GetAuditLogAsync(It.IsAny<GetComplianceAuditLogRequest>()))
                .ReturnsAsync(new List<ComplianceAuditLogEntry>());

            _repositoryMock.Setup(r => r.GetAuditLogCountAsync(It.IsAny<GetComplianceAuditLogRequest>()))
                .ReturnsAsync(0);

            _repositoryMock.Setup(r => r.AddAuditLogEntryAsync(It.IsAny<ComplianceAuditLogEntry>()))
                .Returns(Task.FromResult(true));

            _meteringServiceMock.Setup(m => m.EmitMeteringEvent(It.IsAny<SubscriptionMeteringEvent>()));

            // Act
            var result = await _service.GenerateComplianceEvidenceBundleAsync(request, requestedBy);

            // Assert
            Assert.That(result.Success, Is.True, $"Bundle generation failed: {result.ErrorMessage}");
            Assert.That(result.BundleMetadata, Is.Not.Null);
            Assert.That(result.BundleMetadata!.BundleSha256, Is.Not.Empty);
            Assert.That(result.BundleMetadata.BundleSha256.Length, Is.EqualTo(64)); // SHA256 hex string is 64 characters
            Assert.That(result.BundleMetadata.ComplianceFramework, Is.EqualTo("MICA 2024"));
            Assert.That(result.BundleMetadata.RetentionPeriodYears, Is.EqualTo(7));

            // Verify each file has a checksum
            foreach (var file in result.BundleMetadata.Files)
            {
                Assert.That(file.Sha256, Is.Not.Empty);
                Assert.That(file.Sha256.Length, Is.EqualTo(64)); // SHA256 hex string
                Assert.That(file.Path, Is.Not.Empty);
                Assert.That(file.Description, Is.Not.Empty);
                Assert.That(file.SizeBytes, Is.GreaterThan(0));
            }
        }

        [Test]
        public async Task GenerateComplianceEvidenceBundleAsync_WhitelistServiceError_ShouldReturnFailure()
        {
            // Arrange
            var assetId = 12345ul;
            var requestedBy = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";
            var request = new GenerateComplianceEvidenceBundleRequest 
            { 
                AssetId = assetId,
                IncludeWhitelistHistory = true  // This will trigger the whitelist service call
            };

            // Mock metadata calls to succeed
            _repositoryMock.Setup(r => r.GetMetadataByAssetIdAsync(assetId))
                .ReturnsAsync(new ComplianceMetadata { AssetId = assetId, Network = "testnet" });
            
            // Mock whitelist service to throw an exception
            _whitelistServiceMock.Setup(w => w.ListEntriesAsync(It.IsAny<BiatecTokensApi.Models.Whitelist.ListWhitelistRequest>()))
                .ThrowsAsync(new Exception("Whitelist service unavailable"));
            
            // Mock AddAuditLogEntryAsync to allow the failed log to be recorded
            _repositoryMock.Setup(r => r.AddAuditLogEntryAsync(It.IsAny<ComplianceAuditLogEntry>()))
                .Returns(Task.FromResult(true));

            // Act
            var result = await _service.GenerateComplianceEvidenceBundleAsync(request, requestedBy);

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Is.Not.Null);
            Assert.That(result.ErrorMessage, Does.Contain("Failed to generate compliance evidence bundle"));
            Assert.That(result.ErrorMessage, Does.Contain("Whitelist service unavailable"));

            // Verify failed audit log was attempted
            _repositoryMock.Verify(
                r => r.AddAuditLogEntryAsync(It.Is<ComplianceAuditLogEntry>(
                    e => e.AssetId == assetId &&
                         e.ActionType == ComplianceActionType.Export &&
                         e.Success == false
                )),
                Times.AtLeastOnce);
        }

        #endregion
    }
}

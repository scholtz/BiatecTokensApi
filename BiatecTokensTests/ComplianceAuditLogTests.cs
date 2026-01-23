using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Models.Metering;
using BiatecTokensApi.Repositories;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging;
using Moq;

namespace BiatecTokensTests
{
    [TestFixture]
    public class ComplianceAuditLogTests
    {
        private ComplianceRepository _repository;
        private Mock<ILogger<ComplianceService>> _serviceLoggerMock;
        private Mock<ILogger<ComplianceRepository>> _repoLoggerMock;
        private Mock<ISubscriptionMeteringService> _meteringServiceMock;
        private ComplianceService _service;
        private const string TestPerformedBy = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";
        private const ulong TestAssetId = 12345;

        [SetUp]
        public void Setup()
        {
            _repoLoggerMock = new Mock<ILogger<ComplianceRepository>>();
            _repository = new ComplianceRepository(_repoLoggerMock.Object);
            _serviceLoggerMock = new Mock<ILogger<ComplianceService>>();
            _meteringServiceMock = new Mock<ISubscriptionMeteringService>();
            var whitelistServiceMock = new Mock<IWhitelistService>();
            _service = new ComplianceService(_repository, whitelistServiceMock.Object, _serviceLoggerMock.Object, _meteringServiceMock.Object);
        }

        #region Create Operation Audit Tests

        [Test]
        public async Task UpsertMetadataAsync_CreateNew_ShouldLogAuditEntry()
        {
            // Arrange
            var request = new UpsertComplianceMetadataRequest
            {
                AssetId = TestAssetId,
                KycProvider = "Sumsub",
                VerificationStatus = VerificationStatus.Verified,
                Jurisdiction = "US",
                ComplianceStatus = ComplianceStatus.Compliant,
                Network = "voimain"
            };

            // Act
            var result = await _service.UpsertMetadataAsync(request, TestPerformedBy);

            // Assert - Verify operation succeeded
            Assert.That(result.Success, Is.True);

            // Verify audit log was created
            var auditRequest = new GetComplianceAuditLogRequest
            {
                AssetId = TestAssetId,
                ActionType = ComplianceActionType.Create
            };
            var auditLogs = await _repository.GetAuditLogAsync(auditRequest);

            Assert.That(auditLogs.Count, Is.EqualTo(1), "Should have exactly one create audit log entry");

            var auditEntry = auditLogs[0];
            Assert.That(auditEntry.AssetId, Is.EqualTo(TestAssetId));
            Assert.That(auditEntry.Network, Is.EqualTo("voimain"));
            Assert.That(auditEntry.ActionType, Is.EqualTo(ComplianceActionType.Create));
            Assert.That(auditEntry.PerformedBy, Is.EqualTo(TestPerformedBy));
            Assert.That(auditEntry.Success, Is.True);
            Assert.That(auditEntry.NewComplianceStatus, Is.EqualTo(ComplianceStatus.Compliant));
            Assert.That(auditEntry.NewVerificationStatus, Is.EqualTo(VerificationStatus.Verified));
            Assert.That(auditEntry.OldComplianceStatus, Is.Null);
            Assert.That(auditEntry.OldVerificationStatus, Is.Null);
        }

        [Test]
        public async Task UpsertMetadataAsync_CreateWithValidationError_ShouldLogFailedAuditEntry()
        {
            // Arrange - VOI network requires both jurisdiction and verified status
            var request = new UpsertComplianceMetadataRequest
            {
                AssetId = TestAssetId,
                Network = "voimain",
                VerificationStatus = VerificationStatus.Pending,
                ComplianceStatus = ComplianceStatus.UnderReview,
                Jurisdiction = "US", // Has jurisdiction
                RequiresAccreditedInvestors = true // But not verified - this will fail
            };

            // Act
            var result = await _service.UpsertMetadataAsync(request, TestPerformedBy);

            // Assert - Verify operation failed
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("KYC verification"));

            // Verify failed audit log was created
            var auditRequest = new GetComplianceAuditLogRequest
            {
                AssetId = TestAssetId,
                Success = false
            };
            var auditLogs = await _repository.GetAuditLogAsync(auditRequest);

            Assert.That(auditLogs.Count, Is.EqualTo(1), "Should have exactly one failed audit log entry");

            var auditEntry = auditLogs[0];
            Assert.That(auditEntry.AssetId, Is.EqualTo(TestAssetId));
            Assert.That(auditEntry.Success, Is.False);
            Assert.That(auditEntry.ErrorMessage, Does.Contain("KYC verification"));
        }

        #endregion

        #region Update Operation Audit Tests

        [Test]
        public async Task UpsertMetadataAsync_UpdateExisting_ShouldLogUpdateAuditEntry()
        {
            // Arrange - First create
            var createRequest = new UpsertComplianceMetadataRequest
            {
                AssetId = TestAssetId,
                ComplianceStatus = ComplianceStatus.UnderReview,
                VerificationStatus = VerificationStatus.Pending,
                Network = "testnet"
            };
            await _service.UpsertMetadataAsync(createRequest, TestPerformedBy);

            // Act - Update
            var updateRequest = new UpsertComplianceMetadataRequest
            {
                AssetId = TestAssetId,
                ComplianceStatus = ComplianceStatus.Compliant,
                VerificationStatus = VerificationStatus.Verified,
                Network = "testnet"
            };
            var result = await _service.UpsertMetadataAsync(updateRequest, TestPerformedBy);

            // Assert
            Assert.That(result.Success, Is.True);

            // Verify update audit log was created
            var auditRequest = new GetComplianceAuditLogRequest
            {
                AssetId = TestAssetId,
                ActionType = ComplianceActionType.Update
            };
            var auditLogs = await _repository.GetAuditLogAsync(auditRequest);

            Assert.That(auditLogs.Count, Is.EqualTo(1), "Should have exactly one update audit log entry");

            var auditEntry = auditLogs[0];
            Assert.That(auditEntry.AssetId, Is.EqualTo(TestAssetId));
            Assert.That(auditEntry.ActionType, Is.EqualTo(ComplianceActionType.Update));
            Assert.That(auditEntry.Success, Is.True);
            Assert.That(auditEntry.OldComplianceStatus, Is.EqualTo(ComplianceStatus.UnderReview));
            Assert.That(auditEntry.NewComplianceStatus, Is.EqualTo(ComplianceStatus.Compliant));
            Assert.That(auditEntry.OldVerificationStatus, Is.EqualTo(VerificationStatus.Pending));
            Assert.That(auditEntry.NewVerificationStatus, Is.EqualTo(VerificationStatus.Verified));
        }

        #endregion

        #region Read Operation Audit Tests

        [Test]
        public async Task GetMetadataAsync_Success_ShouldLogReadAuditEntry()
        {
            // Arrange - Create metadata first
            var createRequest = new UpsertComplianceMetadataRequest
            {
                AssetId = TestAssetId,
                Network = "testnet",
                ComplianceStatus = ComplianceStatus.Compliant
            };
            await _service.UpsertMetadataAsync(createRequest, TestPerformedBy);

            // Act - Read
            var result = await _service.GetMetadataAsync(TestAssetId);

            // Assert
            Assert.That(result.Success, Is.True);

            // Verify read audit log was created
            var auditRequest = new GetComplianceAuditLogRequest
            {
                AssetId = TestAssetId,
                ActionType = ComplianceActionType.Read
            };
            var auditLogs = await _repository.GetAuditLogAsync(auditRequest);

            Assert.That(auditLogs.Count, Is.EqualTo(1), "Should have exactly one read audit log entry");

            var auditEntry = auditLogs[0];
            Assert.That(auditEntry.AssetId, Is.EqualTo(TestAssetId));
            Assert.That(auditEntry.ActionType, Is.EqualTo(ComplianceActionType.Read));
            Assert.That(auditEntry.Success, Is.True);
        }

        [Test]
        public async Task GetMetadataAsync_NotFound_ShouldLogFailedReadAuditEntry()
        {
            // Arrange - No metadata exists
            var nonExistentAssetId = (ulong)99999;

            // Act
            var result = await _service.GetMetadataAsync(nonExistentAssetId);

            // Assert
            Assert.That(result.Success, Is.False);

            // Verify failed read audit log was created
            var auditRequest = new GetComplianceAuditLogRequest
            {
                AssetId = nonExistentAssetId,
                ActionType = ComplianceActionType.Read
            };
            var auditLogs = await _repository.GetAuditLogAsync(auditRequest);

            Assert.That(auditLogs.Count, Is.EqualTo(1));

            var auditEntry = auditLogs[0];
            Assert.That(auditEntry.AssetId, Is.EqualTo(nonExistentAssetId));
            Assert.That(auditEntry.Success, Is.False);
            Assert.That(auditEntry.ErrorMessage, Does.Contain("not found"));
        }

        #endregion

        #region Delete Operation Audit Tests

        [Test]
        public async Task DeleteMetadataAsync_Success_ShouldLogDeleteAuditEntry()
        {
            // Arrange - Create metadata first
            var createRequest = new UpsertComplianceMetadataRequest
            {
                AssetId = TestAssetId,
                Network = "testnet",
                ComplianceStatus = ComplianceStatus.Compliant
            };
            await _service.UpsertMetadataAsync(createRequest, TestPerformedBy);

            // Act - Delete
            var result = await _service.DeleteMetadataAsync(TestAssetId);

            // Assert
            Assert.That(result.Success, Is.True);

            // Verify delete audit log was created
            var auditRequest = new GetComplianceAuditLogRequest
            {
                AssetId = TestAssetId,
                ActionType = ComplianceActionType.Delete
            };
            var auditLogs = await _repository.GetAuditLogAsync(auditRequest);

            Assert.That(auditLogs.Count, Is.EqualTo(1), "Should have exactly one delete audit log entry");

            var auditEntry = auditLogs[0];
            Assert.That(auditEntry.AssetId, Is.EqualTo(TestAssetId));
            Assert.That(auditEntry.ActionType, Is.EqualTo(ComplianceActionType.Delete));
            Assert.That(auditEntry.Success, Is.True);
        }

        [Test]
        public async Task DeleteMetadataAsync_NotFound_ShouldLogFailedDeleteAuditEntry()
        {
            // Arrange - No metadata exists
            var nonExistentAssetId = (ulong)99999;

            // Act
            var result = await _service.DeleteMetadataAsync(nonExistentAssetId);

            // Assert
            Assert.That(result.Success, Is.False);

            // Verify failed delete audit log was created
            var auditRequest = new GetComplianceAuditLogRequest
            {
                AssetId = nonExistentAssetId,
                ActionType = ComplianceActionType.Delete
            };
            var auditLogs = await _repository.GetAuditLogAsync(auditRequest);

            Assert.That(auditLogs.Count, Is.EqualTo(1));

            var auditEntry = auditLogs[0];
            Assert.That(auditEntry.AssetId, Is.EqualTo(nonExistentAssetId));
            Assert.That(auditEntry.Success, Is.False);
        }

        #endregion

        #region List Operation Audit Tests

        [Test]
        public async Task ListMetadataAsync_ShouldLogListAuditEntry()
        {
            // Arrange - Create some metadata
            for (ulong i = 1; i <= 3; i++)
            {
                var request = new UpsertComplianceMetadataRequest
                {
                    AssetId = TestAssetId + i,
                    Network = "testnet",
                    ComplianceStatus = ComplianceStatus.Compliant
                };
                await _service.UpsertMetadataAsync(request, TestPerformedBy);
            }

            // Act - List
            var listRequest = new ListComplianceMetadataRequest
            {
                Page = 1,
                PageSize = 10
            };
            var result = await _service.ListMetadataAsync(listRequest);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Metadata.Count, Is.EqualTo(3));

            // Verify list audit log was created
            var auditRequest = new GetComplianceAuditLogRequest
            {
                ActionType = ComplianceActionType.List
            };
            var auditLogs = await _repository.GetAuditLogAsync(auditRequest);

            Assert.That(auditLogs.Count, Is.EqualTo(1), "Should have exactly one list audit log entry");

            var auditEntry = auditLogs[0];
            Assert.That(auditEntry.ActionType, Is.EqualTo(ComplianceActionType.List));
            Assert.That(auditEntry.Success, Is.True);
            Assert.That(auditEntry.ItemCount, Is.EqualTo(3));
        }

        [Test]
        public async Task ListMetadataAsync_WithFilters_ShouldLogFilterCriteria()
        {
            // Arrange
            var listRequest = new ListComplianceMetadataRequest
            {
                ComplianceStatus = ComplianceStatus.Compliant,
                VerificationStatus = VerificationStatus.Verified,
                Network = "voimain",
                Page = 1,
                PageSize = 10
            };

            // Act
            var result = await _service.ListMetadataAsync(listRequest);

            // Assert
            Assert.That(result.Success, Is.True);

            // Verify list audit log includes filter criteria
            var auditRequest = new GetComplianceAuditLogRequest
            {
                ActionType = ComplianceActionType.List
            };
            var auditLogs = await _repository.GetAuditLogAsync(auditRequest);

            Assert.That(auditLogs.Count, Is.EqualTo(1));

            var auditEntry = auditLogs[0];
            Assert.That(auditEntry.FilterCriteria, Does.Contain("ComplianceStatus=Compliant"));
            Assert.That(auditEntry.FilterCriteria, Does.Contain("VerificationStatus=Verified"));
            Assert.That(auditEntry.FilterCriteria, Does.Contain("Network=voimain"));
        }

        #endregion

        #region Audit Log Filtering Tests

        [Test]
        public async Task GetAuditLogAsync_FilterByAssetId_ShouldReturnMatchingEntries()
        {
            // Arrange - Create operations for multiple assets
            for (ulong i = 1; i <= 3; i++)
            {
                var request = new UpsertComplianceMetadataRequest
                {
                    AssetId = TestAssetId + i,
                    Network = "testnet",
                    ComplianceStatus = ComplianceStatus.Compliant
                };
                await _service.UpsertMetadataAsync(request, TestPerformedBy);
            }

            // Act - Filter by specific asset
            var auditRequest = new GetComplianceAuditLogRequest
            {
                AssetId = TestAssetId + 2
            };
            var result = await _service.GetAuditLogAsync(auditRequest);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Entries.All(e => e.AssetId == TestAssetId + 2), Is.True);
        }

        [Test]
        public async Task GetAuditLogAsync_FilterByNetwork_ShouldReturnMatchingEntries()
        {
            // Arrange - Create operations on different networks
            var request1 = new UpsertComplianceMetadataRequest
            {
                AssetId = TestAssetId,
                Network = "voimain",
                ComplianceStatus = ComplianceStatus.Compliant
            };
            await _service.UpsertMetadataAsync(request1, TestPerformedBy);

            var request2 = new UpsertComplianceMetadataRequest
            {
                AssetId = TestAssetId + 1,
                Network = "testnet",
                ComplianceStatus = ComplianceStatus.Compliant
            };
            await _service.UpsertMetadataAsync(request2, TestPerformedBy);

            // Act - Filter by network
            var auditRequest = new GetComplianceAuditLogRequest
            {
                Network = "voimain"
            };
            var result = await _service.GetAuditLogAsync(auditRequest);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Entries.All(e => e.Network == "voimain"), Is.True);
        }

        [Test]
        public async Task GetAuditLogAsync_FilterByActionType_ShouldReturnMatchingEntries()
        {
            // Arrange - Perform various operations
            var request = new UpsertComplianceMetadataRequest
            {
                AssetId = TestAssetId,
                Network = "testnet",
                ComplianceStatus = ComplianceStatus.Compliant
            };
            await _service.UpsertMetadataAsync(request, TestPerformedBy); // Create
            await _service.GetMetadataAsync(TestAssetId); // Read
            await _service.DeleteMetadataAsync(TestAssetId); // Delete

            // Act - Filter by Create action
            var auditRequest = new GetComplianceAuditLogRequest
            {
                ActionType = ComplianceActionType.Create
            };
            var result = await _service.GetAuditLogAsync(auditRequest);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Entries.All(e => e.ActionType == ComplianceActionType.Create), Is.True);
        }

        [Test]
        public async Task GetAuditLogAsync_FilterByPerformedBy_ShouldReturnMatchingEntries()
        {
            // Arrange
            var user1 = "USER1AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";
            var user2 = "USER2AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";

            var request1 = new UpsertComplianceMetadataRequest
            {
                AssetId = TestAssetId,
                Network = "testnet",
                ComplianceStatus = ComplianceStatus.Compliant
            };
            await _service.UpsertMetadataAsync(request1, user1);

            var request2 = new UpsertComplianceMetadataRequest
            {
                AssetId = TestAssetId + 1,
                Network = "testnet",
                ComplianceStatus = ComplianceStatus.Compliant
            };
            await _service.UpsertMetadataAsync(request2, user2);

            // Act - Filter by user
            var auditRequest = new GetComplianceAuditLogRequest
            {
                PerformedBy = user1
            };
            var result = await _service.GetAuditLogAsync(auditRequest);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Entries.All(e => e.PerformedBy == user1), Is.True);
        }

        [Test]
        public async Task GetAuditLogAsync_FilterBySuccess_ShouldReturnMatchingEntries()
        {
            // Arrange - Create successful and failed operations
            var successRequest = new UpsertComplianceMetadataRequest
            {
                AssetId = TestAssetId,
                Network = "testnet",
                ComplianceStatus = ComplianceStatus.Compliant,
                Jurisdiction = "US"
            };
            await _service.UpsertMetadataAsync(successRequest, TestPerformedBy);

            var failRequest = new UpsertComplianceMetadataRequest
            {
                AssetId = TestAssetId + 1,
                Network = "voimain",
                ComplianceStatus = ComplianceStatus.Compliant,
                Jurisdiction = null, // This will fail VOI validation
                RequiresAccreditedInvestors = true
            };
            await _service.UpsertMetadataAsync(failRequest, TestPerformedBy);

            // Act - Filter by failed operations
            var auditRequest = new GetComplianceAuditLogRequest
            {
                Success = false
            };
            var result = await _service.GetAuditLogAsync(auditRequest);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Entries.All(e => e.Success == false), Is.True);
            Assert.That(result.Entries.Count, Is.GreaterThan(0));
        }

        [Test]
        public async Task GetAuditLogAsync_FilterByDateRange_ShouldReturnMatchingEntries()
        {
            // Arrange
            var startDate = DateTime.UtcNow;
            
            var request = new UpsertComplianceMetadataRequest
            {
                AssetId = TestAssetId,
                Network = "testnet",
                ComplianceStatus = ComplianceStatus.Compliant
            };
            await _service.UpsertMetadataAsync(request, TestPerformedBy);

            var endDate = DateTime.UtcNow;

            // Act - Filter by date range
            var auditRequest = new GetComplianceAuditLogRequest
            {
                FromDate = startDate.AddSeconds(-1),
                ToDate = endDate.AddSeconds(1)
            };
            var result = await _service.GetAuditLogAsync(auditRequest);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Entries.Count, Is.GreaterThan(0));
            Assert.That(result.Entries.All(e => e.PerformedAt >= startDate.AddSeconds(-1) && e.PerformedAt <= endDate.AddSeconds(1)), Is.True);
        }

        #endregion

        #region Pagination Tests

        [Test]
        public async Task GetAuditLogAsync_Pagination_ShouldReturnCorrectPage()
        {
            // Arrange - Create multiple operations
            for (int i = 0; i < 10; i++)
            {
                var request = new UpsertComplianceMetadataRequest
                {
                    AssetId = TestAssetId + (ulong)i,
                    Network = "testnet",
                    ComplianceStatus = ComplianceStatus.Compliant
                };
                await _service.UpsertMetadataAsync(request, TestPerformedBy);
                await Task.Delay(10); // Ensure different timestamps
            }

            // Act - Get first page
            var auditRequest1 = new GetComplianceAuditLogRequest
            {
                Page = 1,
                PageSize = 5
            };
            var result1 = await _service.GetAuditLogAsync(auditRequest1);

            // Act - Get second page
            var auditRequest2 = new GetComplianceAuditLogRequest
            {
                Page = 2,
                PageSize = 5
            };
            var result2 = await _service.GetAuditLogAsync(auditRequest2);

            // Assert
            Assert.That(result1.Success, Is.True);
            Assert.That(result1.Entries.Count, Is.EqualTo(5));
            Assert.That(result1.Page, Is.EqualTo(1));
            Assert.That(result1.TotalCount, Is.GreaterThanOrEqualTo(10));
            Assert.That(result1.TotalPages, Is.GreaterThanOrEqualTo(2));

            Assert.That(result2.Success, Is.True);
            Assert.That(result2.Entries.Count, Is.GreaterThan(0));
            Assert.That(result2.Page, Is.EqualTo(2));

            // Ensure no overlap
            var page1Ids = result1.Entries.Select(e => e.Id).ToHashSet();
            var page2Ids = result2.Entries.Select(e => e.Id).ToHashSet();
            Assert.That(page1Ids.Intersect(page2Ids).Count(), Is.EqualTo(0), "Pages should not have overlapping entries");
        }

        #endregion

        #region Retention Policy Tests

        [Test]
        public async Task GetAuditLogAsync_ShouldIncludeRetentionPolicy()
        {
            // Arrange - Create an operation
            var request = new UpsertComplianceMetadataRequest
            {
                AssetId = TestAssetId,
                Network = "testnet",
                ComplianceStatus = ComplianceStatus.Compliant
            };
            await _service.UpsertMetadataAsync(request, TestPerformedBy);

            // Act
            var auditRequest = new GetComplianceAuditLogRequest();
            var result = await _service.GetAuditLogAsync(auditRequest);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.RetentionPolicy, Is.Not.Null);
            Assert.That(result.RetentionPolicy!.MinimumRetentionYears, Is.EqualTo(7));
            Assert.That(result.RetentionPolicy.RegulatoryFramework, Is.EqualTo("MICA"));
            Assert.That(result.RetentionPolicy.ImmutableEntries, Is.True);
            Assert.That(result.RetentionPolicy.Description, Does.Contain("7 years"));
        }

        #endregion

        #region Immutability Tests

        [Test]
        public async Task AuditLogEntries_ShouldBeImmutable()
        {
            // Arrange - Create an operation
            var request = new UpsertComplianceMetadataRequest
            {
                AssetId = TestAssetId,
                Network = "testnet",
                ComplianceStatus = ComplianceStatus.Compliant
            };
            await _service.UpsertMetadataAsync(request, TestPerformedBy);

            // Act - Get audit log
            var auditRequest = new GetComplianceAuditLogRequest
            {
                AssetId = TestAssetId
            };
            var result = await _service.GetAuditLogAsync(auditRequest);

            // Assert - Entries exist and have IDs (cannot be modified after creation)
            Assert.That(result.Entries.Count, Is.GreaterThan(0));
            Assert.That(result.Entries.All(e => !string.IsNullOrEmpty(e.Id)), Is.True);
            Assert.That(result.Entries.All(e => e.PerformedAt <= DateTime.UtcNow), Is.True);
        }

        #endregion

        #region Multiple Operations Test

        [Test]
        public async Task MultipleOperations_ShouldCreateMultipleAuditLogs()
        {
            // Arrange & Act - Perform full CRUD cycle
            var createRequest = new UpsertComplianceMetadataRequest
            {
                AssetId = TestAssetId,
                Network = "testnet",
                ComplianceStatus = ComplianceStatus.UnderReview
            };
            await _service.UpsertMetadataAsync(createRequest, TestPerformedBy); // Create

            var updateRequest = new UpsertComplianceMetadataRequest
            {
                AssetId = TestAssetId,
                Network = "testnet",
                ComplianceStatus = ComplianceStatus.Compliant
            };
            await _service.UpsertMetadataAsync(updateRequest, TestPerformedBy); // Update

            await _service.GetMetadataAsync(TestAssetId); // Read
            await _service.DeleteMetadataAsync(TestAssetId); // Delete

            // Assert - Verify all audit logs were created
            var auditRequest = new GetComplianceAuditLogRequest
            {
                AssetId = TestAssetId
            };
            var result = await _service.GetAuditLogAsync(auditRequest);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Entries.Count, Is.EqualTo(4), "Should have Create, Update, Read, and Delete audit logs");

            var actionTypes = result.Entries.Select(e => e.ActionType).OrderBy(a => a).ToList();
            Assert.That(actionTypes, Contains.Item(ComplianceActionType.Create));
            Assert.That(actionTypes, Contains.Item(ComplianceActionType.Update));
            Assert.That(actionTypes, Contains.Item(ComplianceActionType.Read));
            Assert.That(actionTypes, Contains.Item(ComplianceActionType.Delete));
        }

        #endregion
    }
}

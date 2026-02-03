using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Repositories;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    [TestFixture]
    public class ComplianceReportRepositoryTests
    {
        private ComplianceReportRepository _repository;
        private Mock<ILogger<ComplianceReportRepository>> _loggerMock;

        private const string TestIssuerId = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";
        private const string TestIssuerId2 = "OTHERISSUER1AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";
        private const ulong TestAssetId = 12345;
        private const string TestNetwork = "voimain-v1.0";

        [SetUp]
        public void Setup()
        {
            _loggerMock = new Mock<ILogger<ComplianceReportRepository>>();
            _repository = new ComplianceReportRepository(_loggerMock.Object);
        }

        #region CreateReportAsync Tests

        [Test]
        public async Task CreateReportAsync_ValidReport_ShouldSucceed()
        {
            // Arrange
            var report = new ComplianceReport
            {
                ReportType = ReportType.MicaReadiness,
                IssuerId = TestIssuerId,
                AssetId = TestAssetId,
                Network = TestNetwork
            };

            // Act
            var result = await _repository.CreateReportAsync(report);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.ReportId, Is.Not.Null);
            Assert.That(result.IssuerId, Is.EqualTo(TestIssuerId));
            Assert.That(result.ReportType, Is.EqualTo(ReportType.MicaReadiness));
        }

        [Test]
        public async Task CreateReportAsync_WithoutReportId_ShouldGenerateId()
        {
            // Arrange
            var report = new ComplianceReport
            {
                ReportId = string.Empty, // Empty ID
                IssuerId = TestIssuerId,
                ReportType = ReportType.AuditTrail
            };

            // Act
            var result = await _repository.CreateReportAsync(report);

            // Assert
            Assert.That(result.ReportId, Is.Not.Empty);
            Assert.That(Guid.TryParse(result.ReportId, out _), Is.True);
        }

        [Test]
        public void CreateReportAsync_DuplicateReportId_ShouldThrowException()
        {
            // Arrange
            var reportId = Guid.NewGuid().ToString();
            var report1 = new ComplianceReport
            {
                ReportId = reportId,
                IssuerId = TestIssuerId,
                ReportType = ReportType.MicaReadiness
            };
            var report2 = new ComplianceReport
            {
                ReportId = reportId,
                IssuerId = TestIssuerId,
                ReportType = ReportType.AuditTrail
            };

            // Act
            _repository.CreateReportAsync(report1).Wait();

            // Assert
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _repository.CreateReportAsync(report2));
        }

        #endregion

        #region UpdateReportAsync Tests

        [Test]
        public async Task UpdateReportAsync_ExistingReport_ShouldSucceed()
        {
            // Arrange
            var report = new ComplianceReport
            {
                IssuerId = TestIssuerId,
                ReportType = ReportType.MicaReadiness,
                Status = ReportStatus.Pending
            };

            var created = await _repository.CreateReportAsync(report);
            created.Status = ReportStatus.Completed;
            created.Checksum = "abc123";

            // Act
            var result = await _repository.UpdateReportAsync(created);

            // Assert
            Assert.That(result.Status, Is.EqualTo(ReportStatus.Completed));
            Assert.That(result.Checksum, Is.EqualTo("abc123"));
        }

        [Test]
        public void UpdateReportAsync_NonExistingReport_ShouldThrowException()
        {
            // Arrange
            var report = new ComplianceReport
            {
                ReportId = Guid.NewGuid().ToString(),
                IssuerId = TestIssuerId,
                ReportType = ReportType.MicaReadiness
            };

            // Act & Assert
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _repository.UpdateReportAsync(report));
        }

        #endregion

        #region GetReportAsync Tests

        [Test]
        public async Task GetReportAsync_ExistingReport_ShouldReturnReport()
        {
            // Arrange
            var report = new ComplianceReport
            {
                IssuerId = TestIssuerId,
                ReportType = ReportType.MicaReadiness,
                AssetId = TestAssetId
            };

            var created = await _repository.CreateReportAsync(report);

            // Act
            var result = await _repository.GetReportAsync(created.ReportId, TestIssuerId);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.ReportId, Is.EqualTo(created.ReportId));
            Assert.That(result.AssetId, Is.EqualTo(TestAssetId));
        }

        [Test]
        public async Task GetReportAsync_NonExistingReport_ShouldReturnNull()
        {
            // Arrange
            var reportId = Guid.NewGuid().ToString();

            // Act
            var result = await _repository.GetReportAsync(reportId, TestIssuerId);

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public async Task GetReportAsync_DifferentIssuer_ShouldReturnNull()
        {
            // Arrange
            var report = new ComplianceReport
            {
                IssuerId = TestIssuerId,
                ReportType = ReportType.MicaReadiness
            };

            var created = await _repository.CreateReportAsync(report);

            // Act
            var result = await _repository.GetReportAsync(created.ReportId, TestIssuerId2);

            // Assert
            Assert.That(result, Is.Null); // Access denied due to different issuer
        }

        #endregion

        #region ListReportsAsync Tests

        [Test]
        public async Task ListReportsAsync_NoFilters_ShouldReturnAllReportsForIssuer()
        {
            // Arrange
            await _repository.CreateReportAsync(new ComplianceReport
            {
                IssuerId = TestIssuerId,
                ReportType = ReportType.MicaReadiness
            });
            await _repository.CreateReportAsync(new ComplianceReport
            {
                IssuerId = TestIssuerId,
                ReportType = ReportType.AuditTrail
            });
            await _repository.CreateReportAsync(new ComplianceReport
            {
                IssuerId = TestIssuerId2, // Different issuer
                ReportType = ReportType.MicaReadiness
            });

            var request = new ListComplianceReportsRequest();

            // Act
            var result = await _repository.ListReportsAsync(TestIssuerId, request);

            // Assert
            Assert.That(result.Count, Is.EqualTo(2)); // Only TestIssuerId's reports
        }

        [Test]
        public async Task ListReportsAsync_FilterByReportType_ShouldReturnMatchingReports()
        {
            // Arrange
            await _repository.CreateReportAsync(new ComplianceReport
            {
                IssuerId = TestIssuerId,
                ReportType = ReportType.MicaReadiness
            });
            await _repository.CreateReportAsync(new ComplianceReport
            {
                IssuerId = TestIssuerId,
                ReportType = ReportType.AuditTrail
            });
            await _repository.CreateReportAsync(new ComplianceReport
            {
                IssuerId = TestIssuerId,
                ReportType = ReportType.MicaReadiness
            });

            var request = new ListComplianceReportsRequest
            {
                ReportType = ReportType.MicaReadiness
            };

            // Act
            var result = await _repository.ListReportsAsync(TestIssuerId, request);

            // Assert
            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result.All(r => r.ReportType == ReportType.MicaReadiness), Is.True);
        }

        [Test]
        public async Task ListReportsAsync_FilterByAssetId_ShouldReturnMatchingReports()
        {
            // Arrange
            await _repository.CreateReportAsync(new ComplianceReport
            {
                IssuerId = TestIssuerId,
                AssetId = TestAssetId
            });
            await _repository.CreateReportAsync(new ComplianceReport
            {
                IssuerId = TestIssuerId,
                AssetId = 99999
            });

            var request = new ListComplianceReportsRequest
            {
                AssetId = TestAssetId
            };

            // Act
            var result = await _repository.ListReportsAsync(TestIssuerId, request);

            // Assert
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].AssetId, Is.EqualTo(TestAssetId));
        }

        [Test]
        public async Task ListReportsAsync_FilterByNetwork_ShouldReturnMatchingReports()
        {
            // Arrange
            await _repository.CreateReportAsync(new ComplianceReport
            {
                IssuerId = TestIssuerId,
                Network = TestNetwork
            });
            await _repository.CreateReportAsync(new ComplianceReport
            {
                IssuerId = TestIssuerId,
                Network = "aramidmain-v1.0"
            });

            var request = new ListComplianceReportsRequest
            {
                Network = TestNetwork
            };

            // Act
            var result = await _repository.ListReportsAsync(TestIssuerId, request);

            // Assert
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].Network, Is.EqualTo(TestNetwork));
        }

        [Test]
        public async Task ListReportsAsync_FilterByStatus_ShouldReturnMatchingReports()
        {
            // Arrange
            await _repository.CreateReportAsync(new ComplianceReport
            {
                IssuerId = TestIssuerId,
                Status = ReportStatus.Completed
            });
            await _repository.CreateReportAsync(new ComplianceReport
            {
                IssuerId = TestIssuerId,
                Status = ReportStatus.Failed
            });

            var request = new ListComplianceReportsRequest
            {
                Status = ReportStatus.Completed
            };

            // Act
            var result = await _repository.ListReportsAsync(TestIssuerId, request);

            // Assert
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].Status, Is.EqualTo(ReportStatus.Completed));
        }

        [Test]
        public async Task ListReportsAsync_FilterByDateRange_ShouldReturnMatchingReports()
        {
            // Arrange
            var now = DateTime.UtcNow;
            var oldReport = new ComplianceReport
            {
                IssuerId = TestIssuerId,
                CreatedAt = now.AddDays(-10)
            };
            var recentReport = new ComplianceReport
            {
                IssuerId = TestIssuerId,
                CreatedAt = now.AddDays(-2)
            };

            await _repository.CreateReportAsync(oldReport);
            await _repository.CreateReportAsync(recentReport);

            var request = new ListComplianceReportsRequest
            {
                FromDate = now.AddDays(-5),
                ToDate = now
            };

            // Act
            var result = await _repository.ListReportsAsync(TestIssuerId, request);

            // Assert
            Assert.That(result.Count, Is.EqualTo(1)); // Only the recent report
        }

        [Test]
        public async Task ListReportsAsync_WithPagination_ShouldReturnCorrectPage()
        {
            // Arrange
            for (int i = 0; i < 25; i++)
            {
                await _repository.CreateReportAsync(new ComplianceReport
                {
                    IssuerId = TestIssuerId,
                    ReportType = ReportType.MicaReadiness
                });
            }

            var request = new ListComplianceReportsRequest
            {
                Page = 2,
                PageSize = 10
            };

            // Act
            var result = await _repository.ListReportsAsync(TestIssuerId, request);

            // Assert
            Assert.That(result.Count, Is.EqualTo(10)); // Page 2 with 10 items
        }

        [Test]
        public async Task ListReportsAsync_OrderedByMostRecent_ShouldReturnInCorrectOrder()
        {
            // Arrange
            var report1 = await _repository.CreateReportAsync(new ComplianceReport
            {
                IssuerId = TestIssuerId,
                CreatedAt = DateTime.UtcNow.AddHours(-2)
            });
            var report2 = await _repository.CreateReportAsync(new ComplianceReport
            {
                IssuerId = TestIssuerId,
                CreatedAt = DateTime.UtcNow.AddHours(-1)
            });

            var request = new ListComplianceReportsRequest();

            // Act
            var result = await _repository.ListReportsAsync(TestIssuerId, request);

            // Assert
            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result[0].ReportId, Is.EqualTo(report2.ReportId)); // Most recent first
            Assert.That(result[1].ReportId, Is.EqualTo(report1.ReportId));
        }

        #endregion

        #region GetReportCountAsync Tests

        [Test]
        public async Task GetReportCountAsync_NoFilters_ShouldReturnTotalCount()
        {
            // Arrange
            await _repository.CreateReportAsync(new ComplianceReport { IssuerId = TestIssuerId });
            await _repository.CreateReportAsync(new ComplianceReport { IssuerId = TestIssuerId });
            await _repository.CreateReportAsync(new ComplianceReport { IssuerId = TestIssuerId2 });

            var request = new ListComplianceReportsRequest();

            // Act
            var result = await _repository.GetReportCountAsync(TestIssuerId, request);

            // Assert
            Assert.That(result, Is.EqualTo(2)); // Only TestIssuerId's reports
        }

        [Test]
        public async Task GetReportCountAsync_WithFilters_ShouldReturnFilteredCount()
        {
            // Arrange
            await _repository.CreateReportAsync(new ComplianceReport
            {
                IssuerId = TestIssuerId,
                ReportType = ReportType.MicaReadiness
            });
            await _repository.CreateReportAsync(new ComplianceReport
            {
                IssuerId = TestIssuerId,
                ReportType = ReportType.MicaReadiness
            });
            await _repository.CreateReportAsync(new ComplianceReport
            {
                IssuerId = TestIssuerId,
                ReportType = ReportType.AuditTrail
            });

            var request = new ListComplianceReportsRequest
            {
                ReportType = ReportType.MicaReadiness
            };

            // Act
            var result = await _repository.GetReportCountAsync(TestIssuerId, request);

            // Assert
            Assert.That(result, Is.EqualTo(2));
        }

        #endregion

        #region Thread Safety Tests

        [Test]
        public async Task ConcurrentCreateReports_ShouldHandleThreadSafety()
        {
            // Arrange
            var tasks = new List<Task<ComplianceReport>>();

            // Act - Create 10 reports concurrently
            for (int i = 0; i < 10; i++)
            {
                var task = _repository.CreateReportAsync(new ComplianceReport
                {
                    IssuerId = TestIssuerId,
                    ReportType = ReportType.MicaReadiness
                });
                tasks.Add(task);
            }

            var results = await Task.WhenAll(tasks);

            // Assert
            Assert.That(results.Length, Is.EqualTo(10));
            var uniqueIds = results.Select(r => r.ReportId).Distinct().Count();
            Assert.That(uniqueIds, Is.EqualTo(10)); // All IDs should be unique

            var listRequest = new ListComplianceReportsRequest();
            var list = await _repository.ListReportsAsync(TestIssuerId, listRequest);
            Assert.That(list.Count, Is.EqualTo(10));
        }

        #endregion
    }
}

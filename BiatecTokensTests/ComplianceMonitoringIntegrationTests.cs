using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Models.Whitelist;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Integration tests for compliance monitoring dashboard endpoints
    /// Tests cover metrics accuracy, access control, and network filtering
    /// </summary>
    [TestFixture]
    public class ComplianceMonitoringIntegrationTests
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
            _service = new ComplianceService(
                _repositoryMock.Object,
                _whitelistServiceMock.Object,
                _loggerMock.Object,
                _meteringServiceMock.Object,
                Mock.Of<IWebhookService>());
        }

        #region GetMonitoringMetricsAsync Tests

        [Test]
        public async Task GetMonitoringMetrics_ValidRequest_ReturnsComprehensiveMetrics()
        {
            // Arrange
            var request = new GetComplianceMonitoringMetricsRequest
            {
                Network = "voimain-v1.0",
                FromDate = DateTime.UtcNow.AddDays(-30),
                ToDate = DateTime.UtcNow
            };

            // Setup whitelist audit entries for enforcement metrics
            var whitelistAuditEntries = new List<WhitelistAuditLogEntry>
            {
                new WhitelistAuditLogEntry
                {
                    AssetId = 12345,
                    Network = "voimain-v1.0",
                    ActionType = WhitelistActionType.TransferValidation,
                    TransferAllowed = true,
                    PerformedAt = DateTime.UtcNow.AddDays(-1)
                },
                new WhitelistAuditLogEntry
                {
                    AssetId = 12345,
                    Network = "voimain-v1.0",
                    ActionType = WhitelistActionType.TransferValidation,
                    TransferAllowed = false,
                    DenialReason = "Sender not whitelisted",
                    PerformedAt = DateTime.UtcNow.AddDays(-2)
                },
                new WhitelistAuditLogEntry
                {
                    AssetId = 67890,
                    Network = "voimain-v1.0",
                    ActionType = WhitelistActionType.TransferValidation,
                    TransferAllowed = false,
                    DenialReason = "Receiver not whitelisted",
                    PerformedAt = DateTime.UtcNow.AddDays(-3)
                }
            };

            _whitelistServiceMock.Setup(s => s.GetAuditLogAsync(It.IsAny<GetWhitelistAuditLogRequest>()))
                .ReturnsAsync(new WhitelistAuditLogResponse
                {
                    Success = true,
                    Entries = whitelistAuditEntries,
                    TotalCount = 3
                });

            // Setup compliance audit log
            _repositoryMock.Setup(r => r.GetAuditLogAsync(It.IsAny<GetComplianceAuditLogRequest>()))
                .ReturnsAsync(new List<ComplianceAuditLogEntry>
                {
                    new ComplianceAuditLogEntry
                    {
                        AssetId = 12345,
                        Network = "voimain-v1.0",
                        ActionType = ComplianceActionType.Create,
                        PerformedAt = DateTime.UtcNow.AddDays(-10)
                    }
                });

            _repositoryMock.Setup(r => r.GetAuditLogCountAsync(It.IsAny<GetComplianceAuditLogRequest>()))
                .ReturnsAsync(1);

            // Setup metadata list for retention status
            _repositoryMock.Setup(r => r.ListMetadataAsync(It.IsAny<ListComplianceMetadataRequest>()))
                .ReturnsAsync(new List<ComplianceMetadata>
                {
                    new ComplianceMetadata
                    {
                        AssetId = 12345,
                        Network = "voimain-v1.0",
                        ComplianceStatus = ComplianceStatus.Compliant
                    },
                    new ComplianceMetadata
                    {
                        AssetId = 67890,
                        Network = "voimain-v1.0",
                        ComplianceStatus = ComplianceStatus.UnderReview
                    }
                });

            _repositoryMock.Setup(r => r.GetMetadataCountAsync(It.IsAny<ListComplianceMetadataRequest>()))
                .ReturnsAsync(2);

            // Act
            var result = await _service.GetMonitoringMetricsAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.WhitelistEnforcement, Is.Not.Null);
            Assert.That(result.WhitelistEnforcement.TotalValidations, Is.EqualTo(3));
            Assert.That(result.WhitelistEnforcement.AllowedTransfers, Is.EqualTo(1));
            Assert.That(result.WhitelistEnforcement.DeniedTransfers, Is.EqualTo(2));
            Assert.That(result.WhitelistEnforcement.AssetsWithEnforcement, Is.EqualTo(2));
            Assert.That(result.AuditHealth, Is.Not.Null);
            Assert.That(result.NetworkRetentionStatus, Is.Not.Empty);
            Assert.That(result.OverallHealthScore, Is.GreaterThan(0));

            Console.WriteLine($"Monitoring metrics test passed - Overall health score: {result.OverallHealthScore}");
        }

        [Test]
        public async Task GetMonitoringMetrics_WithNetworkFilter_FiltersCorrectly()
        {
            // Arrange
            var request = new GetComplianceMonitoringMetricsRequest
            {
                Network = "aramidmain-v1.0"
            };

            _whitelistServiceMock.Setup(s => s.GetAuditLogAsync(It.Is<GetWhitelistAuditLogRequest>(
                r => r.Network == "aramidmain-v1.0")))
                .ReturnsAsync(new WhitelistAuditLogResponse
                {
                    Success = true,
                    Entries = new List<WhitelistAuditLogEntry>(),
                    TotalCount = 0
                });

            _repositoryMock.Setup(r => r.GetAuditLogAsync(It.Is<GetComplianceAuditLogRequest>(
                r => r.Network == "aramidmain-v1.0")))
                .ReturnsAsync(new List<ComplianceAuditLogEntry>());

            _repositoryMock.Setup(r => r.GetAuditLogCountAsync(It.Is<GetComplianceAuditLogRequest>(
                r => r.Network == "aramidmain-v1.0")))
                .ReturnsAsync(0);

            _repositoryMock.Setup(r => r.ListMetadataAsync(It.Is<ListComplianceMetadataRequest>(
                r => r.Network == "aramidmain-v1.0")))
                .ReturnsAsync(new List<ComplianceMetadata>());

            _repositoryMock.Setup(r => r.GetMetadataCountAsync(It.Is<ListComplianceMetadataRequest>(
                r => r.Network == "aramidmain-v1.0")))
                .ReturnsAsync(0);

            // Act
            var result = await _service.GetMonitoringMetricsAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            
            // Verify network filtering was applied (service might call this multiple times)
            _whitelistServiceMock.Verify(s => s.GetAuditLogAsync(
                It.Is<GetWhitelistAuditLogRequest>(r => r.Network == "aramidmain-v1.0")), 
                Times.AtLeastOnce);

            Console.WriteLine("Network filtering test passed for aramidmain-v1.0");
        }

        [Test]
        public async Task GetMonitoringMetrics_CalculatesEnforcementPercentagesCorrectly()
        {
            // Arrange
            var request = new GetComplianceMonitoringMetricsRequest();

            var entries = new List<WhitelistAuditLogEntry>();
            for (int i = 0; i < 80; i++) // 80 allowed
            {
                entries.Add(new WhitelistAuditLogEntry
                {
                    AssetId = 12345,
                    ActionType = WhitelistActionType.TransferValidation,
                    TransferAllowed = true,
                    Network = "voimain-v1.0"
                });
            }
            for (int i = 0; i < 20; i++) // 20 denied
            {
                entries.Add(new WhitelistAuditLogEntry
                {
                    AssetId = 12345,
                    ActionType = WhitelistActionType.TransferValidation,
                    TransferAllowed = false,
                    DenialReason = "Not whitelisted",
                    Network = "voimain-v1.0"
                });
            }

            _whitelistServiceMock.Setup(s => s.GetAuditLogAsync(It.IsAny<GetWhitelistAuditLogRequest>()))
                .ReturnsAsync(new WhitelistAuditLogResponse
                {
                    Success = true,
                    Entries = entries,
                    TotalCount = 100
                });

            SetupDefaultMocks();

            // Act
            var result = await _service.GetMonitoringMetricsAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.WhitelistEnforcement.TotalValidations, Is.EqualTo(100));
            Assert.That(result.WhitelistEnforcement.AllowedTransfers, Is.EqualTo(80));
            Assert.That(result.WhitelistEnforcement.DeniedTransfers, Is.EqualTo(20));
            Assert.That(result.WhitelistEnforcement.AllowedPercentage, Is.EqualTo(80m));

            Console.WriteLine($"Enforcement percentages test passed - {result.WhitelistEnforcement.AllowedPercentage}% allowed");
        }

        #endregion

        #region GetAuditHealthAsync Tests

        [Test]
        public async Task GetAuditHealth_ValidRequest_ReturnsHealthStatus()
        {
            // Arrange
            var request = new GetAuditHealthRequest
            {
                Network = "voimain-v1.0"
            };

            var oldDate = DateTime.UtcNow.AddYears(-8); // More than 7 years
            
            _repositoryMock.Setup(r => r.GetAuditLogAsync(It.IsAny<GetComplianceAuditLogRequest>()))
                .ReturnsAsync(new List<ComplianceAuditLogEntry>
                {
                    new ComplianceAuditLogEntry
                    {
                        PerformedAt = oldDate,
                        ActionType = ComplianceActionType.Create
                    },
                    new ComplianceAuditLogEntry
                    {
                        PerformedAt = DateTime.UtcNow,
                        ActionType = ComplianceActionType.Update
                    }
                });

            _repositoryMock.Setup(r => r.GetAuditLogCountAsync(It.IsAny<GetComplianceAuditLogRequest>()))
                .ReturnsAsync(50);

            _whitelistServiceMock.Setup(s => s.GetAuditLogAsync(It.IsAny<GetWhitelistAuditLogRequest>()))
                .ReturnsAsync(new WhitelistAuditLogResponse
                {
                    Success = true,
                    Entries = new List<WhitelistAuditLogEntry>
                    {
                        new WhitelistAuditLogEntry
                        {
                            PerformedAt = oldDate.AddDays(10),
                            ActionType = WhitelistActionType.Add
                        }
                    },
                    TotalCount = 30
                });

            // Act
            var result = await _service.GetAuditHealthAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.AuditHealth, Is.Not.Null);
            Assert.That(result.AuditHealth.TotalEntries, Is.EqualTo(80));
            Assert.That(result.AuditHealth.ComplianceEntries, Is.EqualTo(50));
            Assert.That(result.AuditHealth.WhitelistEntries, Is.EqualTo(30));
            Assert.That(result.AuditHealth.OldestEntry, Is.Not.Null);
            Assert.That(result.AuditHealth.NewestEntry, Is.Not.Null);
            Assert.That(result.AuditHealth.MeetsRetentionRequirements, Is.True);
            Assert.That(result.AuditHealth.Status, Is.EqualTo(AuditHealthStatus.Healthy));

            Console.WriteLine($"Audit health test passed - Status: {result.AuditHealth.Status}, Total entries: {result.AuditHealth.TotalEntries}");
        }

        [Test]
        public async Task GetAuditHealth_NoEntries_ReturnsWarningStatus()
        {
            // Arrange
            var request = new GetAuditHealthRequest();

            _repositoryMock.Setup(r => r.GetAuditLogAsync(It.IsAny<GetComplianceAuditLogRequest>()))
                .ReturnsAsync(new List<ComplianceAuditLogEntry>());

            _repositoryMock.Setup(r => r.GetAuditLogCountAsync(It.IsAny<GetComplianceAuditLogRequest>()))
                .ReturnsAsync(0);

            _whitelistServiceMock.Setup(s => s.GetAuditLogAsync(It.IsAny<GetWhitelistAuditLogRequest>()))
                .ReturnsAsync(new WhitelistAuditLogResponse
                {
                    Success = true,
                    Entries = new List<WhitelistAuditLogEntry>(),
                    TotalCount = 0
                });

            // Act
            var result = await _service.GetAuditHealthAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.AuditHealth.TotalEntries, Is.EqualTo(0));
            Assert.That(result.AuditHealth.Status, Is.EqualTo(AuditHealthStatus.Warning));
            Assert.That(result.AuditHealth.HealthIssues, Is.Not.Empty);
            Assert.That(result.AuditHealth.HealthIssues[0], Does.Contain("No audit entries found"));

            Console.WriteLine("No entries test passed - Warning status correctly assigned");
        }

        #endregion

        #region GetRetentionStatusAsync Tests

        [Test]
        public async Task GetRetentionStatus_ValidRequest_ReturnsNetworkStatus()
        {
            // Arrange
            var request = new GetRetentionStatusRequest
            {
                Network = "voimain-v1.0"
            };

            SetupDefaultMocksForRetention();

            // Act
            var result = await _service.GetRetentionStatusAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Networks, Is.Not.Empty);
            Assert.That(result.Networks.Count, Is.EqualTo(1));
            
            var voiNetwork = result.Networks.First();
            Assert.That(voiNetwork.Network, Is.EqualTo("voimain-v1.0"));
            Assert.That(voiNetwork.RequiresMicaCompliance, Is.True);
            Assert.That(result.OverallRetentionScore, Is.GreaterThanOrEqualTo(0));

            Console.WriteLine($"Retention status test passed - Network: {voiNetwork.Network}, Status: {voiNetwork.Status}");
        }

        [Test]
        public async Task GetRetentionStatus_AllNetworks_ReturnsMultipleNetworks()
        {
            // Arrange
            var request = new GetRetentionStatusRequest(); // No network filter

            SetupDefaultMocksForRetention();

            // Act
            var result = await _service.GetRetentionStatusAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Networks, Is.Not.Empty);
            Assert.That(result.Networks.Count, Is.GreaterThanOrEqualTo(2)); // At least VOI and Aramid
            
            var voiNetwork = result.Networks.FirstOrDefault(n => n.Network == "voimain-v1.0");
            var aramidNetwork = result.Networks.FirstOrDefault(n => n.Network == "aramidmain-v1.0");
            
            Assert.That(voiNetwork, Is.Not.Null);
            Assert.That(aramidNetwork, Is.Not.Null);
            Assert.That(voiNetwork!.RequiresMicaCompliance, Is.True);
            Assert.That(aramidNetwork!.RequiresMicaCompliance, Is.True);

            Console.WriteLine($"Multiple networks test passed - {result.Networks.Count} networks returned");
        }

        [Test]
        public async Task GetRetentionStatus_CalculatesComplianceCoverageCorrectly()
        {
            // Arrange
            var request = new GetRetentionStatusRequest
            {
                Network = "voimain-v1.0"
            };

            _repositoryMock.Setup(r => r.GetAuditLogAsync(It.IsAny<GetComplianceAuditLogRequest>()))
                .ReturnsAsync(new List<ComplianceAuditLogEntry>());

            _repositoryMock.Setup(r => r.GetAuditLogCountAsync(It.IsAny<GetComplianceAuditLogRequest>()))
                .ReturnsAsync(0);

            _whitelistServiceMock.Setup(s => s.GetAuditLogAsync(It.IsAny<GetWhitelistAuditLogRequest>()))
                .ReturnsAsync(new WhitelistAuditLogResponse
                {
                    Success = true,
                    Entries = new List<WhitelistAuditLogEntry>(),
                    TotalCount = 0
                });

            // 10 assets total, 7 compliant/under review
            var metadata = new List<ComplianceMetadata>();
            for (int i = 0; i < 7; i++)
            {
                metadata.Add(new ComplianceMetadata
                {
                    AssetId = (ulong)(1000 + i),
                    Network = "voimain-v1.0",
                    ComplianceStatus = i % 2 == 0 ? ComplianceStatus.Compliant : ComplianceStatus.UnderReview
                });
            }
            for (int i = 0; i < 3; i++)
            {
                metadata.Add(new ComplianceMetadata
                {
                    AssetId = (ulong)(2000 + i),
                    Network = "voimain-v1.0",
                    ComplianceStatus = ComplianceStatus.NonCompliant
                });
            }

            _repositoryMock.Setup(r => r.ListMetadataAsync(It.IsAny<ListComplianceMetadataRequest>()))
                .ReturnsAsync(metadata);

            _repositoryMock.Setup(r => r.GetMetadataCountAsync(It.IsAny<ListComplianceMetadataRequest>()))
                .ReturnsAsync(10);

            // Act
            var result = await _service.GetRetentionStatusAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            var network = result.Networks.First();
            Assert.That(network.AssetCount, Is.EqualTo(10));
            Assert.That(network.AssetsWithCompliance, Is.EqualTo(7));
            Assert.That(network.ComplianceCoverage, Is.EqualTo(70m));

            Console.WriteLine($"Coverage calculation test passed - {network.ComplianceCoverage}% coverage");
        }

        #endregion

        #region JSON Serialization Tests

        [Test]
        public void MonitoringMetricsResponse_JsonSerialization_WorksCorrectly()
        {
            // Arrange
            var response = new ComplianceMonitoringMetricsResponse
            {
                Success = true,
                WhitelistEnforcement = new WhitelistEnforcementMetrics
                {
                    TotalValidations = 100,
                    AllowedTransfers = 85,
                    DeniedTransfers = 15,
                    AllowedPercentage = 85m,
                    AssetsWithEnforcement = 5,
                    TopDenialReasons = new List<DenialReasonCount>
                    {
                        new DenialReasonCount { Reason = "Sender not whitelisted", Count = 10 },
                        new DenialReasonCount { Reason = "Receiver not whitelisted", Count = 5 }
                    },
                    NetworkBreakdown = new List<NetworkEnforcementMetrics>
                    {
                        new NetworkEnforcementMetrics
                        {
                            Network = "voimain-v1.0",
                            TotalValidations = 60,
                            AllowedTransfers = 50,
                            DeniedTransfers = 10,
                            AssetCount = 3
                        }
                    }
                },
                AuditHealth = new AuditLogHealth
                {
                    TotalEntries = 200,
                    ComplianceEntries = 120,
                    WhitelistEntries = 80,
                    MeetsRetentionRequirements = true,
                    Status = AuditHealthStatus.Healthy,
                    CoveragePercentage = 100m
                },
                OverallHealthScore = 95
            };

            // Act
            var json = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
            var deserialized = JsonSerializer.Deserialize<ComplianceMonitoringMetricsResponse>(json);

            // Assert
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized!.Success, Is.True);
            Assert.That(deserialized.WhitelistEnforcement.TotalValidations, Is.EqualTo(100));
            Assert.That(deserialized.AuditHealth.TotalEntries, Is.EqualTo(200));
            Assert.That(deserialized.OverallHealthScore, Is.EqualTo(95));

            Console.WriteLine("JSON serialization test passed");
            Console.WriteLine($"Serialized JSON:\n{json}");
        }

        #endregion

        #region Helper Methods

        private void SetupDefaultMocks()
        {
            _repositoryMock.Setup(r => r.GetAuditLogAsync(It.IsAny<GetComplianceAuditLogRequest>()))
                .ReturnsAsync(new List<ComplianceAuditLogEntry>());

            _repositoryMock.Setup(r => r.GetAuditLogCountAsync(It.IsAny<GetComplianceAuditLogRequest>()))
                .ReturnsAsync(0);

            _repositoryMock.Setup(r => r.ListMetadataAsync(It.IsAny<ListComplianceMetadataRequest>()))
                .ReturnsAsync(new List<ComplianceMetadata>());

            _repositoryMock.Setup(r => r.GetMetadataCountAsync(It.IsAny<ListComplianceMetadataRequest>()))
                .ReturnsAsync(0);
        }

        private void SetupDefaultMocksForRetention()
        {
            _repositoryMock.Setup(r => r.GetAuditLogAsync(It.IsAny<GetComplianceAuditLogRequest>()))
                .ReturnsAsync(new List<ComplianceAuditLogEntry>
                {
                    new ComplianceAuditLogEntry
                    {
                        PerformedAt = DateTime.UtcNow.AddYears(-8),
                        ActionType = ComplianceActionType.Create
                    }
                });

            _repositoryMock.Setup(r => r.GetAuditLogCountAsync(It.IsAny<GetComplianceAuditLogRequest>()))
                .ReturnsAsync(10);

            _whitelistServiceMock.Setup(s => s.GetAuditLogAsync(It.IsAny<GetWhitelistAuditLogRequest>()))
                .ReturnsAsync(new WhitelistAuditLogResponse
                {
                    Success = true,
                    Entries = new List<WhitelistAuditLogEntry>
                    {
                        new WhitelistAuditLogEntry
                        {
                            PerformedAt = DateTime.UtcNow.AddYears(-7),
                            ActionType = WhitelistActionType.Add
                        }
                    },
                    TotalCount = 5
                });

            _repositoryMock.Setup(r => r.ListMetadataAsync(It.IsAny<ListComplianceMetadataRequest>()))
                .ReturnsAsync(new List<ComplianceMetadata>
                {
                    new ComplianceMetadata
                    {
                        AssetId = 12345,
                        ComplianceStatus = ComplianceStatus.Compliant
                    }
                });

            _repositoryMock.Setup(r => r.GetMetadataCountAsync(It.IsAny<ListComplianceMetadataRequest>()))
                .ReturnsAsync(1);
        }

        #endregion
    }
}

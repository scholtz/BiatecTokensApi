using BiatecTokensApi.Models.Compliance;
using System.Text.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Integration tests for the compliance reporting endpoint
    /// These tests verify JSON serialization, deserialization, and end-to-end scenarios
    /// </summary>
    [TestFixture]
    public class ComplianceReportIntegrationTests
    {
        [Test]
        public void ComplianceReportRequest_JsonSerialization_WorksCorrectly()
        {
            // Arrange
            var request = new GetTokenComplianceReportRequest
            {
                AssetId = 12345,
                Network = "voimain-v1.0",
                FromDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                ToDate = new DateTime(2026, 1, 31, 23, 59, 59, DateTimeKind.Utc),
                IncludeWhitelistDetails = true,
                IncludeTransferAudits = true,
                IncludeComplianceAudits = true,
                MaxAuditEntriesPerCategory = 100,
                Page = 1,
                PageSize = 50
            };

            // Act
            var json = JsonSerializer.Serialize(request);
            var deserialized = JsonSerializer.Deserialize<GetTokenComplianceReportRequest>(json);

            // Assert
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized!.AssetId, Is.EqualTo(12345));
            Assert.That(deserialized.Network, Is.EqualTo("voimain-v1.0"));
            Assert.That(deserialized.IncludeWhitelistDetails, Is.True);
            Assert.That(deserialized.IncludeTransferAudits, Is.True);
            Assert.That(deserialized.IncludeComplianceAudits, Is.True);
            
            Console.WriteLine("ComplianceReportRequest JSON serialization test successful");
            Console.WriteLine($"Serialized: {json}");
        }

        [Test]
        public void ComplianceReportResponse_JsonSerialization_WorksCorrectly()
        {
            // Arrange
            var response = new TokenComplianceReportResponse
            {
                Success = true,
                Tokens = new List<TokenComplianceStatus>
                {
                    new TokenComplianceStatus
                    {
                        AssetId = 12345,
                        Network = "voimain-v1.0",
                        ComplianceMetadata = new ComplianceMetadata
                        {
                            AssetId = 12345,
                            Network = "voimain-v1.0",
                            ComplianceStatus = ComplianceStatus.Compliant,
                            VerificationStatus = VerificationStatus.Verified,
                            Jurisdiction = "US,EU",
                            RegulatoryFramework = "MICA",
                            KycProvider = "Sumsub"
                        },
                        ComplianceHealthScore = 95,
                        Warnings = new List<string>(),
                        NetworkSpecificStatus = new NetworkComplianceStatus
                        {
                            MeetsNetworkRequirements = true,
                            SatisfiedRules = new List<string> { "VOI: KYC verification present" },
                            ViolatedRules = new List<string>(),
                            Recommendations = new List<string>()
                        }
                    }
                },
                TotalCount = 1,
                Page = 1,
                PageSize = 50,
                TotalPages = 1,
                GeneratedAt = DateTime.UtcNow,
                NetworkFilter = "voimain-v1.0",
                SubscriptionInfo = new ReportSubscriptionInfo
                {
                    TierName = "Enterprise",
                    AuditLogEnabled = true,
                    MaxAssetsPerReport = 100,
                    DetailedReportsEnabled = true,
                    Metered = true
                }
            };

            // Act
            var json = JsonSerializer.Serialize(response);
            var deserialized = JsonSerializer.Deserialize<TokenComplianceReportResponse>(json);

            // Assert
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized!.Success, Is.True);
            Assert.That(deserialized.Tokens.Count, Is.EqualTo(1));
            Assert.That(deserialized.Tokens[0].AssetId, Is.EqualTo(12345));
            Assert.That(deserialized.Tokens[0].ComplianceHealthScore, Is.EqualTo(95));
            Assert.That(deserialized.NetworkFilter, Is.EqualTo("voimain-v1.0"));
            Assert.That(deserialized.SubscriptionInfo, Is.Not.Null);
            Assert.That(deserialized.SubscriptionInfo!.TierName, Is.EqualTo("Enterprise"));
            
            Console.WriteLine("ComplianceReportResponse JSON serialization test successful");
            Console.WriteLine($"Serialized tokens count: {deserialized.Tokens.Count}");
        }

        [Test]
        public void TokenComplianceStatus_AllFields_SerializeCorrectly()
        {
            // Arrange
            var status = new TokenComplianceStatus
            {
                AssetId = 12345,
                Network = "aramidmain-v1.0",
                ComplianceMetadata = new ComplianceMetadata
                {
                    AssetId = 12345,
                    ComplianceStatus = ComplianceStatus.Compliant,
                    VerificationStatus = VerificationStatus.Verified
                },
                WhitelistSummary = new WhitelistSummary
                {
                    TotalAddresses = 150,
                    ActiveAddresses = 145,
                    RevokedAddresses = 3,
                    SuspendedAddresses = 2,
                    KycVerifiedAddresses = 140,
                    TransferValidationsCount = 523,
                    DeniedTransfersCount = 8
                },
                ComplianceHealthScore = 88,
                Warnings = new List<string> { "Test warning" },
                NetworkSpecificStatus = new NetworkComplianceStatus
                {
                    MeetsNetworkRequirements = true,
                    SatisfiedRules = new List<string> { "Aramid: Regulatory framework specified" },
                    ViolatedRules = new List<string>(),
                    Recommendations = new List<string>()
                }
            };

            // Act
            var json = JsonSerializer.Serialize(status);
            var deserialized = JsonSerializer.Deserialize<TokenComplianceStatus>(json);

            // Assert
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized!.AssetId, Is.EqualTo(12345));
            Assert.That(deserialized.Network, Is.EqualTo("aramidmain-v1.0"));
            Assert.That(deserialized.WhitelistSummary, Is.Not.Null);
            Assert.That(deserialized.WhitelistSummary!.TotalAddresses, Is.EqualTo(150));
            Assert.That(deserialized.ComplianceHealthScore, Is.EqualTo(88));
            Assert.That(deserialized.Warnings.Count, Is.EqualTo(1));
        }

        [Test]
        public void WhitelistSummary_JsonSerialization_WorksCorrectly()
        {
            // Arrange
            var summary = new WhitelistSummary
            {
                TotalAddresses = 200,
                ActiveAddresses = 190,
                RevokedAddresses = 5,
                SuspendedAddresses = 5,
                KycVerifiedAddresses = 180,
                LastModified = new DateTime(2026, 1, 20, 10, 30, 0, DateTimeKind.Utc),
                TransferValidationsCount = 1000,
                DeniedTransfersCount = 15
            };

            // Act
            var json = JsonSerializer.Serialize(summary);
            var deserialized = JsonSerializer.Deserialize<WhitelistSummary>(json);

            // Assert
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized!.TotalAddresses, Is.EqualTo(200));
            Assert.That(deserialized.ActiveAddresses, Is.EqualTo(190));
            Assert.That(deserialized.KycVerifiedAddresses, Is.EqualTo(180));
            Assert.That(deserialized.TransferValidationsCount, Is.EqualTo(1000));
            Assert.That(deserialized.DeniedTransfersCount, Is.EqualTo(15));
        }

        [Test]
        public void NetworkComplianceStatus_JsonSerialization_WorksCorrectly()
        {
            // Arrange
            var status = new NetworkComplianceStatus
            {
                MeetsNetworkRequirements = false,
                SatisfiedRules = new List<string>
                {
                    "VOI: KYC verification present"
                },
                ViolatedRules = new List<string>
                {
                    "VOI: Jurisdiction not specified"
                },
                Recommendations = new List<string>
                {
                    "Specify jurisdiction to meet VOI network requirements"
                }
            };

            // Act
            var json = JsonSerializer.Serialize(status);
            var deserialized = JsonSerializer.Deserialize<NetworkComplianceStatus>(json);

            // Assert
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized!.MeetsNetworkRequirements, Is.False);
            Assert.That(deserialized.SatisfiedRules.Count, Is.EqualTo(1));
            Assert.That(deserialized.ViolatedRules.Count, Is.EqualTo(1));
            Assert.That(deserialized.Recommendations.Count, Is.EqualTo(1));
        }

        [Test]
        public void ReportSubscriptionInfo_JsonSerialization_WorksCorrectly()
        {
            // Arrange
            var info = new ReportSubscriptionInfo
            {
                TierName = "Premium",
                AuditLogEnabled = true,
                MaxAssetsPerReport = 50,
                DetailedReportsEnabled = true,
                LimitationMessage = "Upgrade to Enterprise for unlimited assets",
                Metered = true
            };

            // Act
            var json = JsonSerializer.Serialize(info);
            var deserialized = JsonSerializer.Deserialize<ReportSubscriptionInfo>(json);

            // Assert
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized!.TierName, Is.EqualTo("Premium"));
            Assert.That(deserialized.AuditLogEnabled, Is.True);
            Assert.That(deserialized.MaxAssetsPerReport, Is.EqualTo(50));
            Assert.That(deserialized.DetailedReportsEnabled, Is.True);
            Assert.That(deserialized.Metered, Is.True);
        }

        [Test]
        public void ComplianceReport_VOINetworkExample_SerializesToValidJson()
        {
            // Arrange - Example VOI network compliance report
            var response = new TokenComplianceReportResponse
            {
                Success = true,
                Tokens = new List<TokenComplianceStatus>
                {
                    new TokenComplianceStatus
                    {
                        AssetId = 100001,
                        Network = "voimain-v1.0",
                        ComplianceMetadata = new ComplianceMetadata
                        {
                            AssetId = 100001,
                            Network = "voimain-v1.0",
                            KycProvider = "Sumsub",
                            VerificationStatus = VerificationStatus.Verified,
                            Jurisdiction = "US",
                            RegulatoryFramework = "SEC Reg D",
                            ComplianceStatus = ComplianceStatus.Compliant,
                            AssetType = "Security Token",
                            RequiresAccreditedInvestors = true,
                            MaxHolders = 500
                        },
                        ComplianceHealthScore = 100,
                        Warnings = new List<string>(),
                        NetworkSpecificStatus = new NetworkComplianceStatus
                        {
                            MeetsNetworkRequirements = true,
                            SatisfiedRules = new List<string>
                            {
                                "VOI: KYC verification present for accredited investor tokens",
                                "VOI: Jurisdiction specified for compliance tracking"
                            },
                            ViolatedRules = new List<string>(),
                            Recommendations = new List<string>()
                        }
                    }
                },
                TotalCount = 1,
                Page = 1,
                PageSize = 50,
                TotalPages = 1,
                GeneratedAt = new DateTime(2026, 1, 23, 10, 0, 0, DateTimeKind.Utc),
                NetworkFilter = "voimain-v1.0",
                SubscriptionInfo = new ReportSubscriptionInfo
                {
                    TierName = "Enterprise",
                    AuditLogEnabled = true,
                    MaxAssetsPerReport = 100,
                    DetailedReportsEnabled = true,
                    Metered = true
                }
            };

            // Act
            var json = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });

            // Assert
            Assert.That(json, Is.Not.Null.And.Not.Empty);
            Assert.That(json, Does.Contain("voimain-v1.0"));
            Assert.That(json, Does.Contain("ComplianceHealthScore"));
            Assert.That(json, Does.Contain("NetworkSpecificStatus"));
            
            Console.WriteLine("VOI Network Compliance Report Example:");
            Console.WriteLine(json);
        }

        [Test]
        public void ComplianceReport_AramidNetworkExample_SerializesToValidJson()
        {
            // Arrange - Example Aramid network compliance report
            var response = new TokenComplianceReportResponse
            {
                Success = true,
                Tokens = new List<TokenComplianceStatus>
                {
                    new TokenComplianceStatus
                    {
                        AssetId = 200001,
                        Network = "aramidmain-v1.0",
                        ComplianceMetadata = new ComplianceMetadata
                        {
                            AssetId = 200001,
                            Network = "aramidmain-v1.0",
                            KycProvider = "Jumio",
                            VerificationStatus = VerificationStatus.Verified,
                            Jurisdiction = "EU",
                            RegulatoryFramework = "MICA",
                            ComplianceStatus = ComplianceStatus.Compliant,
                            AssetType = "Security Token",
                            MaxHolders = 1000
                        },
                        ComplianceHealthScore = 100,
                        Warnings = new List<string>(),
                        NetworkSpecificStatus = new NetworkComplianceStatus
                        {
                            MeetsNetworkRequirements = true,
                            SatisfiedRules = new List<string>
                            {
                                "Aramid: Regulatory framework specified for compliant tokens",
                                "Aramid: MaxHolders specified for security tokens"
                            },
                            ViolatedRules = new List<string>(),
                            Recommendations = new List<string>()
                        }
                    }
                },
                TotalCount = 1,
                Page = 1,
                PageSize = 50,
                TotalPages = 1,
                GeneratedAt = new DateTime(2026, 1, 23, 10, 0, 0, DateTimeKind.Utc),
                NetworkFilter = "aramidmain-v1.0",
                SubscriptionInfo = new ReportSubscriptionInfo
                {
                    TierName = "Enterprise",
                    AuditLogEnabled = true,
                    MaxAssetsPerReport = 100,
                    DetailedReportsEnabled = true,
                    Metered = true
                }
            };

            // Act
            var json = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });

            // Assert
            Assert.That(json, Is.Not.Null.And.Not.Empty);
            Assert.That(json, Does.Contain("aramidmain-v1.0"));
            Assert.That(json, Does.Contain("MICA"));
            Assert.That(json, Does.Contain("ComplianceHealthScore"));
            
            Console.WriteLine("Aramid Network Compliance Report Example:");
            Console.WriteLine(json);
        }

        [Test]
        public void ComplianceReport_WithWarnings_SerializesCorrectly()
        {
            // Arrange - Token with compliance warnings
            var response = new TokenComplianceReportResponse
            {
                Success = true,
                Tokens = new List<TokenComplianceStatus>
                {
                    new TokenComplianceStatus
                    {
                        AssetId = 300001,
                        Network = "voimain-v1.0",
                        ComplianceMetadata = new ComplianceMetadata
                        {
                            AssetId = 300001,
                            Network = "voimain-v1.0",
                            VerificationStatus = VerificationStatus.Expired,
                            ComplianceStatus = ComplianceStatus.UnderReview,
                            NextComplianceReview = DateTime.UtcNow.AddDays(-5)
                        },
                        ComplianceHealthScore = 45,
                        Warnings = new List<string>
                        {
                            "KYC verification has expired and needs renewal",
                            "Compliance review is overdue"
                        },
                        NetworkSpecificStatus = new NetworkComplianceStatus
                        {
                            MeetsNetworkRequirements = false,
                            SatisfiedRules = new List<string>(),
                            ViolatedRules = new List<string>
                            {
                                "VOI: Jurisdiction not specified"
                            },
                            Recommendations = new List<string>
                            {
                                "Renew KYC verification immediately",
                                "Schedule compliance review",
                                "Specify jurisdiction to meet VOI network requirements"
                            }
                        }
                    }
                },
                TotalCount = 1,
                Page = 1,
                PageSize = 50,
                TotalPages = 1,
                NetworkFilter = "voimain-v1.0"
            };

            // Act
            var json = JsonSerializer.Serialize(response);
            var deserialized = JsonSerializer.Deserialize<TokenComplianceReportResponse>(json);

            // Assert
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized!.Tokens[0].Warnings.Count, Is.EqualTo(2));
            Assert.That(deserialized.Tokens[0].ComplianceHealthScore, Is.EqualTo(45));
            Assert.That(deserialized.Tokens[0].NetworkSpecificStatus, Is.Not.Null);
            Assert.That(deserialized.Tokens[0].NetworkSpecificStatus!.MeetsNetworkRequirements, Is.False);
            Assert.That(deserialized.Tokens[0].NetworkSpecificStatus!.ViolatedRules.Count, Is.EqualTo(1));
            Assert.That(deserialized.Tokens[0].NetworkSpecificStatus!.Recommendations.Count, Is.EqualTo(3));
        }

        [Test]
        public void ComplianceReport_MultipleTokens_SerializesCorrectly()
        {
            // Arrange - Multiple tokens in a single report
            var response = new TokenComplianceReportResponse
            {
                Success = true,
                Tokens = new List<TokenComplianceStatus>
                {
                    new TokenComplianceStatus
                    {
                        AssetId = 10001,
                        Network = "voimain-v1.0",
                        ComplianceHealthScore = 95
                    },
                    new TokenComplianceStatus
                    {
                        AssetId = 10002,
                        Network = "voimain-v1.0",
                        ComplianceHealthScore = 88
                    },
                    new TokenComplianceStatus
                    {
                        AssetId = 10003,
                        Network = "voimain-v1.0",
                        ComplianceHealthScore = 72
                    }
                },
                TotalCount = 10,
                Page = 1,
                PageSize = 3,
                TotalPages = 4,
                NetworkFilter = "voimain-v1.0"
            };

            // Act
            var json = JsonSerializer.Serialize(response);
            var deserialized = JsonSerializer.Deserialize<TokenComplianceReportResponse>(json);

            // Assert
            Assert.That(deserialized, Is.Not.Null);
            Assert.That(deserialized!.Tokens.Count, Is.EqualTo(3));
            Assert.That(deserialized.TotalCount, Is.EqualTo(10));
            Assert.That(deserialized.TotalPages, Is.EqualTo(4));
            Assert.That(deserialized.Tokens[0].AssetId, Is.EqualTo(10001));
            Assert.That(deserialized.Tokens[1].AssetId, Is.EqualTo(10002));
            Assert.That(deserialized.Tokens[2].AssetId, Is.EqualTo(10003));
        }
    }
}

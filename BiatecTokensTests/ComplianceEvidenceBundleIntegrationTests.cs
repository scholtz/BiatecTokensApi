using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Models.Metering;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging;
using Moq;
using System.IO.Compression;
using System.Text.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Integration tests for the compliance evidence bundle export endpoint
    /// These tests verify ZIP structure, manifest format, and end-to-end scenarios
    /// </summary>
    [TestFixture]
    public class ComplianceEvidenceBundleIntegrationTests
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

        [Test]
        public async Task EvidenceBundle_ZipStructure_ContainsExpectedFiles()
        {
            // Arrange
            var assetId = 12345ul;
            var requestedBy = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";
            var request = new GenerateComplianceEvidenceBundleRequest
            {
                AssetId = assetId,
                IncludeWhitelistHistory = true,
                IncludeAuditLogs = true,
                IncludePolicyMetadata = true,
                IncludeTokenMetadata = true
            };

            // Setup mocks
            _repositoryMock.Setup(r => r.GetMetadataByAssetIdAsync(assetId))
                .ReturnsAsync(new ComplianceMetadata 
                { 
                    AssetId = assetId, 
                    Network = "voimain-v1.0",
                    ComplianceStatus = ComplianceStatus.Compliant,
                    KycProvider = "Sumsub"
                });

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
                            Status = BiatecTokensApi.Models.Whitelist.WhitelistStatus.Active,
                            CreatedAt = DateTime.UtcNow.AddDays(-10)
                        }
                    }
                });

            _whitelistServiceMock.Setup(w => w.GetAuditLogAsync(It.IsAny<BiatecTokensApi.Models.Whitelist.GetWhitelistAuditLogRequest>()))
                .ReturnsAsync(new BiatecTokensApi.Models.Whitelist.WhitelistAuditLogResponse 
                { 
                    Success = true, 
                    Entries = new List<BiatecTokensApi.Models.Whitelist.WhitelistAuditLogEntry>() 
                });

            _repositoryMock.Setup(r => r.GetAuditLogAsync(It.IsAny<GetComplianceAuditLogRequest>()))
                .ReturnsAsync(new List<ComplianceAuditLogEntry>
                {
                    new ComplianceAuditLogEntry
                    {
                        AssetId = assetId,
                        ActionType = ComplianceActionType.Create,
                        PerformedBy = requestedBy,
                        Success = true,
                        PerformedAt = DateTime.UtcNow.AddDays(-5)
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
            Assert.That(result.ZipContent, Is.Not.Null);
            Assert.That(result.ZipContent!.Length, Is.GreaterThan(0));

            // Extract and verify ZIP contents
            using var memoryStream = new MemoryStream(result.ZipContent);
            using var archive = new ZipArchive(memoryStream, ZipArchiveMode.Read);

            // Verify required files exist
            Assert.That(archive.GetEntry("manifest.json"), Is.Not.Null, "manifest.json should exist");
            Assert.That(archive.GetEntry("README.txt"), Is.Not.Null, "README.txt should exist");
            Assert.That(archive.GetEntry("metadata/compliance_metadata.json"), Is.Not.Null, "compliance_metadata.json should exist");
            Assert.That(archive.GetEntry("whitelist/current_entries.json"), Is.Not.Null, "current_entries.json should exist");
            Assert.That(archive.GetEntry("whitelist/audit_log.json"), Is.Not.Null, "audit_log.json should exist");
            Assert.That(archive.GetEntry("audit_logs/compliance_operations.json"), Is.Not.Null, "compliance_operations.json should exist");
            Assert.That(archive.GetEntry("policy/retention_policy.json"), Is.Not.Null, "retention_policy.json should exist");

            Console.WriteLine($"✅ ZIP bundle contains all expected files ({archive.Entries.Count} total entries)");
        }

        [Test]
        public async Task EvidenceBundle_ManifestFormat_IsValidJson()
        {
            // Arrange
            var assetId = 12345ul;
            var requestedBy = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";
            var request = new GenerateComplianceEvidenceBundleRequest { AssetId = assetId };

            // Setup minimal mocks
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
            Assert.That(result.Success, Is.True);

            // Extract manifest
            using var memoryStream = new MemoryStream(result.ZipContent!);
            using var archive = new ZipArchive(memoryStream, ZipArchiveMode.Read);
            var manifestEntry = archive.GetEntry("manifest.json");
            Assert.That(manifestEntry, Is.Not.Null);

            using var manifestStream = manifestEntry!.Open();
            using var reader = new StreamReader(manifestStream);
            var manifestJson = await reader.ReadToEndAsync();

            // Verify JSON is valid and can be deserialized
            ComplianceEvidenceBundleMetadata? manifest = null;
            Assert.DoesNotThrow(() => 
            {
                manifest = JsonSerializer.Deserialize<ComplianceEvidenceBundleMetadata>(manifestJson);
            }, "Manifest should be valid JSON");

            Assert.That(manifest, Is.Not.Null);
            Assert.That(manifest!.BundleId, Is.Not.Empty);
            Assert.That(manifest.AssetId, Is.EqualTo(assetId));
            Assert.That(manifest.GeneratedBy, Is.EqualTo(requestedBy));
            // Note: BundleSha256 in the manifest inside the ZIP is "pending" because the checksum
            // can only be calculated after the ZIP is complete. The actual checksum is in the response.
            Assert.That(manifest.BundleSha256, Is.EqualTo("pending"));
            Assert.That(result.BundleMetadata!.BundleSha256.Length, Is.EqualTo(64), "Response should have actual 64-char SHA256");
            Assert.That(manifest.ComplianceFramework, Is.EqualTo("MICA 2024"));
            Assert.That(manifest.RetentionPeriodYears, Is.EqualTo(7));
            Assert.That(manifest.Files, Is.Not.Empty);

            // Verify each file has required metadata
            foreach (var file in manifest.Files)
            {
                Assert.That(file.Path, Is.Not.Empty, "File path should not be empty");
                Assert.That(file.Description, Is.Not.Empty, "File description should not be empty");
                Assert.That(file.Sha256, Is.Not.Empty, "File SHA256 should not be empty");
                Assert.That(file.Sha256.Length, Is.EqualTo(64), $"File {file.Path} SHA256 should be 64 hex characters");
                Assert.That(file.SizeBytes, Is.GreaterThan(0), $"File {file.Path} size should be greater than 0");
                Assert.That(file.Format, Is.Not.Empty, "File format should not be empty");
            }

            Console.WriteLine($"✅ Manifest is valid JSON with {manifest.Files.Count} files");
            Console.WriteLine($"   Bundle ID: {manifest.BundleId}");
            Console.WriteLine($"   Bundle SHA256: {manifest.BundleSha256}");
        }

        [Test]
        public async Task EvidenceBundle_FileChecksums_AreCorrect()
        {
            // Arrange
            var assetId = 12345ul;
            var requestedBy = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";
            var request = new GenerateComplianceEvidenceBundleRequest { AssetId = assetId };

            // Setup mocks
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
            Assert.That(result.Success, Is.True);

            // Extract manifest and files
            using var memoryStream = new MemoryStream(result.ZipContent!);
            using var archive = new ZipArchive(memoryStream, ZipArchiveMode.Read);
            var manifestEntry = archive.GetEntry("manifest.json");
            using var manifestStream = manifestEntry!.Open();
            using var reader = new StreamReader(manifestStream);
            var manifestJson = await reader.ReadToEndAsync();
            var manifest = JsonSerializer.Deserialize<ComplianceEvidenceBundleMetadata>(manifestJson);

            // Verify checksums for each file (except manifest and README which aren't in the files list)
            int verifiedFiles = 0;
            foreach (var fileMetadata in manifest!.Files)
            {
                var entry = archive.GetEntry(fileMetadata.Path);
                Assert.That(entry, Is.Not.Null, $"File {fileMetadata.Path} should exist in ZIP");

                using var fileStream = entry!.Open();
                using var memStream = new MemoryStream();
                await fileStream.CopyToAsync(memStream);
                var fileBytes = memStream.ToArray();

                // Calculate SHA256
                var hash = System.Security.Cryptography.SHA256.HashData(fileBytes);
                var calculatedChecksum = BitConverter.ToString(hash).Replace("-", "").ToLower();

                Assert.That(calculatedChecksum, Is.EqualTo(fileMetadata.Sha256), 
                    $"Checksum mismatch for {fileMetadata.Path}");
                
                verifiedFiles++;
            }

            Console.WriteLine($"✅ Verified checksums for {verifiedFiles} files");
        }

        [Test]
        public async Task EvidenceBundle_DateRangeFilter_AppliesCorrectly()
        {
            // Arrange
            var assetId = 12345ul;
            var requestedBy = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";
            var fromDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var toDate = new DateTime(2025, 12, 31, 23, 59, 59, DateTimeKind.Utc);
            var request = new GenerateComplianceEvidenceBundleRequest
            {
                AssetId = assetId,
                FromDate = fromDate,
                ToDate = toDate
            };

            // Setup mocks
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

            // Assert - verify date filters were passed to service calls
            _whitelistServiceMock.Verify(
                w => w.GetAuditLogAsync(It.Is<BiatecTokensApi.Models.Whitelist.GetWhitelistAuditLogRequest>(
                    req => req.FromDate == fromDate && req.ToDate == toDate
                )),
                Times.AtLeastOnce,
                "Whitelist audit log should be called with date filter");

            _repositoryMock.Verify(
                r => r.GetAuditLogAsync(It.Is<GetComplianceAuditLogRequest>(
                    req => req.FromDate == fromDate && req.ToDate == toDate
                )),
                Times.AtLeastOnce,
                "Compliance audit log should be called with date filter");

            // Verify manifest contains date range
            Assert.That(result.BundleMetadata, Is.Not.Null);
            Assert.That(result.BundleMetadata!.FromDate, Is.EqualTo(fromDate));
            Assert.That(result.BundleMetadata.ToDate, Is.EqualTo(toDate));

            Console.WriteLine($"✅ Date range filter applied correctly: {fromDate:yyyy-MM-dd} to {toDate:yyyy-MM-dd}");
        }
    }
}

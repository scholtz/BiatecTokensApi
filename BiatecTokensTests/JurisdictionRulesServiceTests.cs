using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Repositories;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging;
using Moq;
using System.Linq;

namespace BiatecTokensTests
{
    /// <summary>
    /// Unit tests for jurisdiction rules service
    /// </summary>
    [TestFixture]
    public class JurisdictionRulesServiceTests
    {
        private Mock<ILogger<JurisdictionRulesRepository>> _mockRepoLogger;
        private Mock<ILogger<JurisdictionRulesService>> _mockServiceLogger;
        private Mock<IComplianceRepository> _mockComplianceRepo;
        private IJurisdictionRulesRepository _repository;
        private IJurisdictionRulesService _service;

        [SetUp]
        public void Setup()
        {
            _mockRepoLogger = new Mock<ILogger<JurisdictionRulesRepository>>();
            _mockServiceLogger = new Mock<ILogger<JurisdictionRulesService>>();
            _mockComplianceRepo = new Mock<IComplianceRepository>();
            
            _repository = new JurisdictionRulesRepository(_mockRepoLogger.Object);
            _service = new JurisdictionRulesService(_repository, _mockComplianceRepo.Object, _mockServiceLogger.Object);
        }

        [Test]
        public async Task CreateRule_ValidRequest_ReturnsSuccess()
        {
            // Arrange
            var request = new CreateJurisdictionRuleRequest
            {
                JurisdictionCode = "US",
                JurisdictionName = "United States",
                RegulatoryFramework = "SEC",
                IsActive = true,
                Priority = 100,
                Requirements = new List<ComplianceRequirement>
                {
                    new ComplianceRequirement
                    {
                        RequirementCode = "SEC_ACCREDITED",
                        Category = "Disclosure",
                        Description = "Accredited investor verification required",
                        IsMandatory = true,
                        Severity = RequirementSeverity.Critical
                    }
                }
            };

            // Act
            var result = await _service.CreateRuleAsync(request, "testuser");

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Rule, Is.Not.Null);
            Assert.That(result.Rule.JurisdictionCode, Is.EqualTo("US"));
            Assert.That(result.Rule.JurisdictionName, Is.EqualTo("United States"));
            Assert.That(result.Rule.RegulatoryFramework, Is.EqualTo("SEC"));
            Assert.That(result.Rule.Requirements.Count, Is.EqualTo(1));
        }

        [Test]
        public async Task CreateRule_DuplicateJurisdiction_ReturnsFail()
        {
            // Arrange
            var request1 = new CreateJurisdictionRuleRequest
            {
                JurisdictionCode = "UK",
                JurisdictionName = "United Kingdom",
                RegulatoryFramework = "FCA",
                IsActive = true
            };

            var request2 = new CreateJurisdictionRuleRequest
            {
                JurisdictionCode = "UK",
                JurisdictionName = "United Kingdom Updated",
                RegulatoryFramework = "FCA",
                IsActive = true
            };

            // Act
            await _service.CreateRuleAsync(request1, "testuser");
            var result = await _service.CreateRuleAsync(request2, "testuser");

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("already exists"));
        }

        [Test]
        public async Task ListRules_DefaultSeededRules_ReturnsEUAndGlobal()
        {
            // Arrange
            var request = new ListJurisdictionRulesRequest
            {
                Page = 1,
                PageSize = 50
            };

            // Act
            var result = await _service.ListRulesAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.TotalCount, Is.EqualTo(2)); // EU MICA and GLOBAL baseline
            Assert.That(result.Rules.Any(r => r.JurisdictionCode == "EU"), Is.True);
            Assert.That(result.Rules.Any(r => r.JurisdictionCode == "GLOBAL"), Is.True);
        }

        [Test]
        public async Task ListRules_FilterByJurisdiction_ReturnsFilteredResults()
        {
            // Arrange
            var request = new ListJurisdictionRulesRequest
            {
                JurisdictionCode = "EU",
                Page = 1,
                PageSize = 50
            };

            // Act
            var result = await _service.ListRulesAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Rules.Count, Is.EqualTo(1));
            Assert.That(result.Rules[0].JurisdictionCode, Is.EqualTo("EU"));
        }

        [Test]
        public async Task ListRules_FilterByRegulatoryFramework_ReturnsFilteredResults()
        {
            // Arrange
            var request = new ListJurisdictionRulesRequest
            {
                RegulatoryFramework = "MICA",
                Page = 1,
                PageSize = 50
            };

            // Act
            var result = await _service.ListRulesAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Rules.Count, Is.EqualTo(1));
            Assert.That(result.Rules[0].RegulatoryFramework, Is.EqualTo("MICA"));
        }

        [Test]
        public async Task GetRuleById_ExistingRule_ReturnsRule()
        {
            // Arrange
            var createRequest = new CreateJurisdictionRuleRequest
            {
                JurisdictionCode = "FR",
                JurisdictionName = "France",
                RegulatoryFramework = "MICA",
                IsActive = true
            };

            var createResult = await _service.CreateRuleAsync(createRequest, "testuser");

            // Act
            var result = await _service.GetRuleByIdAsync(createResult.Rule!.Id);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Rule, Is.Not.Null);
            Assert.That(result.Rule.JurisdictionCode, Is.EqualTo("FR"));
        }

        [Test]
        public async Task GetRuleById_NonExistingRule_ReturnsFail()
        {
            // Act
            var result = await _service.GetRuleByIdAsync("nonexistent-id");

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("not found"));
        }

        [Test]
        public async Task UpdateRule_ExistingRule_ReturnsSuccess()
        {
            // Arrange
            var createRequest = new CreateJurisdictionRuleRequest
            {
                JurisdictionCode = "DE",
                JurisdictionName = "Germany",
                RegulatoryFramework = "MICA",
                IsActive = true
            };

            var createResult = await _service.CreateRuleAsync(createRequest, "testuser");

            var updateRequest = new CreateJurisdictionRuleRequest
            {
                JurisdictionCode = "DE",
                JurisdictionName = "Germany (Updated)",
                RegulatoryFramework = "MICA",
                IsActive = false
            };

            // Act
            var result = await _service.UpdateRuleAsync(createResult.Rule!.Id, updateRequest, "updateuser");

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Rule, Is.Not.Null);
            Assert.That(result.Rule.JurisdictionName, Is.EqualTo("Germany (Updated)"));
            Assert.That(result.Rule.IsActive, Is.False);
            Assert.That(result.Rule.UpdatedBy, Is.EqualTo("updateuser"));
        }

        [Test]
        public async Task UpdateRule_NonExistingRule_ReturnsFail()
        {
            // Arrange
            var updateRequest = new CreateJurisdictionRuleRequest
            {
                JurisdictionCode = "XX",
                JurisdictionName = "Test",
                RegulatoryFramework = "TEST",
                IsActive = true
            };

            // Act
            var result = await _service.UpdateRuleAsync("nonexistent-id", updateRequest, "testuser");

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("not found"));
        }

        [Test]
        public async Task DeleteRule_ExistingRule_ReturnsSuccess()
        {
            // Arrange
            var createRequest = new CreateJurisdictionRuleRequest
            {
                JurisdictionCode = "IT",
                JurisdictionName = "Italy",
                RegulatoryFramework = "MICA",
                IsActive = true
            };

            var createResult = await _service.CreateRuleAsync(createRequest, "testuser");

            // Act
            var result = await _service.DeleteRuleAsync(createResult.Rule!.Id);

            // Assert
            Assert.That(result.Success, Is.True);

            // Verify it's deleted
            var getResult = await _service.GetRuleByIdAsync(createResult.Rule.Id);
            Assert.That(getResult.Success, Is.False);
        }

        [Test]
        public async Task DeleteRule_NonExistingRule_ReturnsFail()
        {
            // Act
            var result = await _service.DeleteRuleAsync("nonexistent-id");

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("not found"));
        }

        [Test]
        public async Task AssignTokenJurisdiction_ValidJurisdiction_ReturnsSuccess()
        {
            // Arrange
            ulong assetId = 12345;
            string network = "voimain-v1.0";

            // Act
            var result = await _service.AssignTokenJurisdictionAsync(assetId, network, "EU", true, "testuser", "Test assignment");

            // Assert
            Assert.That(result.Success, Is.True);

            // Verify assignment
            var jurisdictions = await _service.GetTokenJurisdictionsAsync(assetId, network);
            Assert.That(jurisdictions.Count, Is.EqualTo(1));
            Assert.That(jurisdictions[0].JurisdictionCode, Is.EqualTo("EU"));
            Assert.That(jurisdictions[0].IsPrimary, Is.True);
        }

        [Test]
        public async Task AssignTokenJurisdiction_InvalidJurisdiction_ReturnsFail()
        {
            // Arrange
            ulong assetId = 12345;
            string network = "voimain-v1.0";

            // Act
            var result = await _service.AssignTokenJurisdictionAsync(assetId, network, "INVALID", true, "testuser");

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("not found"));
        }

        [Test]
        public async Task AssignTokenJurisdiction_MultiplePrimary_OnlyOneRemainsPrimary()
        {
            // Arrange
            ulong assetId = 12345;
            string network = "voimain-v1.0";

            // Act
            await _service.AssignTokenJurisdictionAsync(assetId, network, "EU", true, "testuser");
            await _service.AssignTokenJurisdictionAsync(assetId, network, "GLOBAL", true, "testuser");

            // Assert
            var jurisdictions = await _service.GetTokenJurisdictionsAsync(assetId, network);
            Assert.That(jurisdictions.Count, Is.EqualTo(2));
            
            // Only GLOBAL should be primary (last assigned)
            var primaryCount = jurisdictions.Count(j => j.IsPrimary);
            Assert.That(primaryCount, Is.EqualTo(1));
            Assert.That(jurisdictions.First(j => j.JurisdictionCode == "GLOBAL").IsPrimary, Is.True);
        }

        [Test]
        public async Task RemoveTokenJurisdiction_ExistingAssignment_ReturnsSuccess()
        {
            // Arrange
            ulong assetId = 12345;
            string network = "voimain-v1.0";
            await _service.AssignTokenJurisdictionAsync(assetId, network, "EU", true, "testuser");

            // Act
            var result = await _service.RemoveTokenJurisdictionAsync(assetId, network, "EU");

            // Assert
            Assert.That(result.Success, Is.True);

            // Verify removal
            var jurisdictions = await _service.GetTokenJurisdictionsAsync(assetId, network);
            Assert.That(jurisdictions, Is.Empty);
        }

        [Test]
        public async Task RemoveTokenJurisdiction_NonExistingAssignment_ReturnsFail()
        {
            // Arrange
            ulong assetId = 12345;
            string network = "voimain-v1.0";

            // Act
            var result = await _service.RemoveTokenJurisdictionAsync(assetId, network, "EU");

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("not found"));
        }

        [Test]
        public async Task EvaluateTokenCompliance_NoJurisdictionAssigned_UsesGlobalBaseline()
        {
            // Arrange
            ulong assetId = 12345;
            string network = "voimain-v1.0";
            string issuerId = "testissuer";

            _mockComplianceRepo.Setup(r => r.GetMetadataByAssetIdAsync(assetId))
                .ReturnsAsync((ComplianceMetadata?)null);

            // Act
            var result = await _service.EvaluateTokenComplianceAsync(assetId, network, issuerId);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.AssetId, Is.EqualTo(assetId));
            Assert.That(result.Network, Is.EqualTo(network));
            Assert.That(result.ApplicableJurisdictions.Any(j => j == "GLOBAL"), Is.True);
            Assert.That(result.CheckResults, Is.Not.Empty);
        }

        [Test]
        public async Task EvaluateTokenCompliance_WithKYCVerified_PassesKYCCheck()
        {
            // Arrange
            ulong assetId = 12345;
            string network = "voimain-v1.0";
            string issuerId = "testissuer";

            var metadata = new ComplianceMetadata
            {
                AssetId = assetId,
                Network = network,
                KycProvider = "TestKYC",
                KycVerificationDate = DateTime.UtcNow.AddDays(-10),
                VerificationStatus = VerificationStatus.Verified
            };

            _mockComplianceRepo.Setup(r => r.GetMetadataByAssetIdAsync(assetId))
                .ReturnsAsync(metadata);

            // Act
            var result = await _service.EvaluateTokenComplianceAsync(assetId, network, issuerId);

            // Assert
            Assert.That(result, Is.Not.Null);
            var kycCheck = result.CheckResults.FirstOrDefault(c => c.RequirementCode == "FATF_KYC");
            Assert.That(kycCheck, Is.Not.Null);
            Assert.That(kycCheck.Status, Is.EqualTo("Pass"));
        }

        [Test]
        public async Task EvaluateTokenCompliance_WithoutKYC_FailsKYCCheck()
        {
            // Arrange
            ulong assetId = 12345;
            string network = "voimain-v1.0";
            string issuerId = "testissuer";

            var metadata = new ComplianceMetadata
            {
                AssetId = assetId,
                Network = network,
                VerificationStatus = VerificationStatus.Pending
            };

            _mockComplianceRepo.Setup(r => r.GetMetadataByAssetIdAsync(assetId))
                .ReturnsAsync(metadata);

            // Act
            var result = await _service.EvaluateTokenComplianceAsync(assetId, network, issuerId);

            // Assert
            Assert.That(result, Is.Not.Null);
            var kycCheck = result.CheckResults.FirstOrDefault(c => c.RequirementCode == "FATF_KYC");
            Assert.That(kycCheck, Is.Not.Null);
            Assert.That(kycCheck.Status, Is.EqualTo("Fail"));
        }

        [Test]
        public async Task EvaluateTokenCompliance_EUJurisdiction_EvaluatesMICARequirements()
        {
            // Arrange
            ulong assetId = 12345;
            string network = "voimain-v1.0";
            string issuerId = "testissuer";

            await _service.AssignTokenJurisdictionAsync(assetId, network, "EU", true, "testuser");

            _mockComplianceRepo.Setup(r => r.GetMetadataByAssetIdAsync(assetId))
                .ReturnsAsync((ComplianceMetadata?)null);

            // Act
            var result = await _service.EvaluateTokenComplianceAsync(assetId, network, issuerId);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.ApplicableJurisdictions.Any(j => j == "EU"), Is.True);
            Assert.That(result.CheckResults.Any(c => c.RequirementCode == "MICA_ARTICLE_20"), Is.True);
            Assert.That(result.CheckResults.Any(c => c.RequirementCode == "MICA_ARTICLE_23"), Is.True);
        }

        [Test]
        public async Task EvaluateTokenCompliance_AllChecksFail_NonCompliantStatus()
        {
            // Arrange
            ulong assetId = 12345;
            string network = "voimain-v1.0";
            string issuerId = "testissuer";

            var metadata = new ComplianceMetadata
            {
                AssetId = assetId,
                Network = network,
                VerificationStatus = VerificationStatus.Pending
            };

            _mockComplianceRepo.Setup(r => r.GetMetadataByAssetIdAsync(assetId))
                .ReturnsAsync(metadata);

            // Act
            var result = await _service.EvaluateTokenComplianceAsync(assetId, network, issuerId);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.ComplianceStatus, Is.EqualTo("NonCompliant"));
            Assert.That(result.Rationale, Is.Not.Empty);
        }

        [Test]
        public async Task ListRules_Pagination_WorksCorrectly()
        {
            // Arrange - Create 5 rules
            for (int i = 0; i < 5; i++)
            {
                var request = new CreateJurisdictionRuleRequest
                {
                    JurisdictionCode = $"T{i}",
                    JurisdictionName = $"Test{i}",
                    RegulatoryFramework = "TEST",
                    IsActive = true,
                    Priority = 100 + i
                };
                await _service.CreateRuleAsync(request, "testuser");
            }

            // Act - Get first page with 3 items
            var page1Request = new ListJurisdictionRulesRequest { Page = 1, PageSize = 3 };
            var page1Result = await _service.ListRulesAsync(page1Request);

            // Act - Get second page with 3 items
            var page2Request = new ListJurisdictionRulesRequest { Page = 2, PageSize = 3 };
            var page2Result = await _service.ListRulesAsync(page2Request);

            // Assert
            Assert.That(page1Result.Success, Is.True);
            Assert.That(page1Result.Rules.Count, Is.EqualTo(3));
            Assert.That(page1Result.TotalCount, Is.EqualTo(7)); // 5 created + 2 default (EU, GLOBAL)
            Assert.That(page1Result.TotalPages, Is.EqualTo(3));

            Assert.That(page2Result.Success, Is.True);
            Assert.That(page2Result.Rules.Count, Is.EqualTo(3));
        }
    }
}

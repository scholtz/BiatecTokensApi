using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System.Text.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Unit tests for ValidationService
    /// </summary>
    [TestFixture]
    public class ValidationServiceTests
    {
        private Mock<IComplianceRepository> _repositoryMock = null!;
        private Mock<ILogger<ValidationService>> _loggerMock = null!;
        private ValidationService _service = null!;

        private const string TestUserAddress = "TESTUSER3AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

        [SetUp]
        public void Setup()
        {
            _repositoryMock = new Mock<IComplianceRepository>();
            _loggerMock = new Mock<ILogger<ValidationService>>();
            _service = new ValidationService(_repositoryMock.Object, _loggerMock.Object);
        }

        #region ASA Token Validation Tests

        [Test]
        public async Task ValidateTokenMetadataAsync_ValidASAToken_ShouldPass()
        {
            // Arrange
            var metadata = new Dictionary<string, object?>
            {
                { "AssetName", "Test Token" },
                { "UnitName", "TEST" },
                { "Total", 1000000UL },
                { "Decimals", 6U }
            };

            var request = new ValidateTokenMetadataRequest
            {
                Context = new ValidationContext
                {
                    Network = "mainnet",
                    TokenStandard = "ASA"
                },
                TokenMetadata = metadata,
                DryRun = true
            };

            // Act
            var result = await _service.ValidateTokenMetadataAsync(request, TestUserAddress);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Passed, Is.True);
            Assert.That(result.Evidence, Is.Not.Null);
            Assert.That(result.Evidence.TotalRules, Is.GreaterThan(0));
            Assert.That(result.Evidence.FailedRules, Is.EqualTo(0));
            Assert.That(result.Evidence.Checksum, Is.Not.Empty);
        }

        [Test]
        public async Task ValidateTokenMetadataAsync_ASAMissingAssetName_ShouldFail()
        {
            // Arrange
            var metadata = new Dictionary<string, object?>
            {
                { "UnitName", "TEST" },
                { "Total", 1000000UL },
                { "Decimals", 6U }
            };

            var request = new ValidateTokenMetadataRequest
            {
                Context = new ValidationContext
                {
                    Network = "mainnet",
                    TokenStandard = "ASA"
                },
                TokenMetadata = metadata,
                DryRun = true
            };

            // Act
            var result = await _service.ValidateTokenMetadataAsync(request, TestUserAddress);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Passed, Is.False);
            Assert.That(result.Evidence, Is.Not.Null);
            Assert.That(result.Evidence.FailedRules, Is.GreaterThan(0));

            var failedRule = result.Evidence.RuleEvaluations.FirstOrDefault(r => r.RuleId == "ASA-001");
            Assert.That(failedRule, Is.Not.Null);
            Assert.That(failedRule.Passed, Is.False);
            Assert.That(failedRule.ErrorMessage, Does.Contain("required"));
        }

        [Test]
        public async Task ValidateTokenMetadataAsync_ASAAssetNameTooLong_ShouldFail()
        {
            // Arrange
            var metadata = new Dictionary<string, object?>
            {
                { "AssetName", "This is a very long asset name that exceeds the maximum of 32 characters" },
                { "UnitName", "TEST" },
                { "Total", 1000000UL },
                { "Decimals", 6U }
            };

            var request = new ValidateTokenMetadataRequest
            {
                Context = new ValidationContext
                {
                    Network = "mainnet",
                    TokenStandard = "ASA"
                },
                TokenMetadata = metadata,
                DryRun = true
            };

            // Act
            var result = await _service.ValidateTokenMetadataAsync(request, TestUserAddress);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Passed, Is.False);
            Assert.That(result.Evidence, Is.Not.Null);

            var failedRule = result.Evidence.RuleEvaluations.FirstOrDefault(r => r.RuleId == "ASA-001");
            Assert.That(failedRule, Is.Not.Null);
            Assert.That(failedRule.Passed, Is.False);
            Assert.That(failedRule.ErrorMessage, Does.Contain("too long"));
        }

        [Test]
        public async Task ValidateTokenMetadataAsync_ASAInvalidNetwork_ShouldFail()
        {
            // Arrange
            var metadata = new Dictionary<string, object?>
            {
                { "AssetName", "Test Token" },
                { "UnitName", "TEST" },
                { "Total", 1000000UL },
                { "Decimals", 6U }
            };

            var request = new ValidateTokenMetadataRequest
            {
                Context = new ValidationContext
                {
                    Network = "invalid-network",
                    TokenStandard = "ASA"
                },
                TokenMetadata = metadata,
                DryRun = true
            };

            // Act
            var result = await _service.ValidateTokenMetadataAsync(request, TestUserAddress);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Passed, Is.False);

            var failedRule = result.Evidence!.RuleEvaluations.FirstOrDefault(r => r.RuleId == "ASA-005");
            Assert.That(failedRule, Is.Not.Null);
            Assert.That(failedRule.Passed, Is.False);
            Assert.That(failedRule.ErrorMessage, Does.Contain("Invalid network"));
        }

        #endregion

        #region ARC3 Token Validation Tests

        [Test]
        public async Task ValidateTokenMetadataAsync_ValidARC3Token_ShouldPass()
        {
            // Arrange
            var metadata = new Dictionary<string, object?>
            {
                { "AssetName", "NFT Token" },
                { "UnitName", "NFT" },
                { "Total", 1UL },
                { "Decimals", 0U },
                { "URL", "ipfs://QmTest..." }
            };

            var request = new ValidateTokenMetadataRequest
            {
                Context = new ValidationContext
                {
                    Network = "mainnet",
                    TokenStandard = "ARC3"
                },
                TokenMetadata = metadata,
                DryRun = true
            };

            // Act
            var result = await _service.ValidateTokenMetadataAsync(request, TestUserAddress);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Passed, Is.True);
            Assert.That(result.Evidence, Is.Not.Null);
        }

        [Test]
        public async Task ValidateTokenMetadataAsync_ARC3MissingIPFSUrl_ShouldFail()
        {
            // Arrange
            var metadata = new Dictionary<string, object?>
            {
                { "AssetName", "NFT Token" },
                { "UnitName", "NFT" },
                { "Total", 1UL },
                { "Decimals", 0U }
            };

            var request = new ValidateTokenMetadataRequest
            {
                Context = new ValidationContext
                {
                    Network = "mainnet",
                    TokenStandard = "ARC3"
                },
                TokenMetadata = metadata,
                DryRun = true
            };

            // Act
            var result = await _service.ValidateTokenMetadataAsync(request, TestUserAddress);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Evidence, Is.Not.Null);
            Assert.That(result.Evidence.RuleEvaluations, Is.Not.Empty);
            
            // Check that ARC3-001 rule exists and failed
            var failedRule = result.Evidence.RuleEvaluations.FirstOrDefault(r => r.RuleId == "ARC3-001");
            Assert.That(failedRule, Is.Not.Null, "ARC3-001 rule should exist");
            Assert.That(failedRule.Passed, Is.False, "ARC3-001 rule should have failed");
            Assert.That(failedRule.Severity, Is.EqualTo(ValidationSeverity.Error), "ARC3-001 should be Error severity");
            
            // Overall validation should fail because there's an Error severity rule that failed
            Assert.That(result.Passed, Is.False, "Overall validation should fail when Error severity rule fails");
            Assert.That(failedRule.ErrorMessage, Does.Contain("IPFS"));
        }

        #endregion

        #region ERC20 Token Validation Tests

        [Test]
        public async Task ValidateTokenMetadataAsync_ValidERC20Token_ShouldPass()
        {
            // Arrange
            var metadata = new Dictionary<string, object?>
            {
                { "Name", "Test ERC20 Token" },
                { "Symbol", "TEST" },
                { "TotalSupply", 1000000UL }
            };

            var request = new ValidateTokenMetadataRequest
            {
                Context = new ValidationContext
                {
                    Network = "base",
                    TokenStandard = "ERC20"
                },
                TokenMetadata = metadata,
                DryRun = true
            };

            // Act
            var result = await _service.ValidateTokenMetadataAsync(request, TestUserAddress);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Passed, Is.True);
            Assert.That(result.Evidence, Is.Not.Null);
        }

        [Test]
        public async Task ValidateTokenMetadataAsync_ERC20MissingSymbol_ShouldFail()
        {
            // Arrange
            var metadata = new Dictionary<string, object?>
            {
                { "Name", "Test ERC20 Token" },
                { "TotalSupply", 1000000UL }
            };

            var request = new ValidateTokenMetadataRequest
            {
                Context = new ValidationContext
                {
                    Network = "base",
                    TokenStandard = "ERC20"
                },
                TokenMetadata = metadata,
                DryRun = true
            };

            // Act
            var result = await _service.ValidateTokenMetadataAsync(request, TestUserAddress);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Passed, Is.False);

            var failedRule = result.Evidence!.RuleEvaluations.FirstOrDefault(r => r.RuleId == "ERC20-002");
            Assert.That(failedRule, Is.Not.Null);
            Assert.That(failedRule.Passed, Is.False);
        }

        #endregion

        #region Evidence Storage and Retrieval Tests

        [Test]
        public async Task ValidateTokenMetadataAsync_NotDryRun_ShouldPersistEvidence()
        {
            // Arrange
            var metadata = new Dictionary<string, object?>
            {
                { "AssetName", "Test Token" },
                { "UnitName", "TEST" },
                { "Total", 1000000UL },
                { "Decimals", 6U }
            };

            var request = new ValidateTokenMetadataRequest
            {
                Context = new ValidationContext
                {
                    Network = "mainnet",
                    TokenStandard = "ASA"
                },
                TokenMetadata = metadata,
                DryRun = false,
                PreIssuanceId = "test-pre-issuance-123"
            };

            _repositoryMock.Setup(r => r.StoreValidationEvidenceAsync(It.IsAny<ValidationEvidence>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.ValidateTokenMetadataAsync(request, TestUserAddress);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.EvidenceId, Is.Not.Null);
            _repositoryMock.Verify(r => r.StoreValidationEvidenceAsync(It.Is<ValidationEvidence>(e =>
                e.PreIssuanceId == "test-pre-issuance-123" &&
                e.RequestedBy == TestUserAddress &&
                !e.IsDryRun &&
                e.Checksum != string.Empty
            )), Times.Once);
        }

        [Test]
        public async Task ValidateTokenMetadataAsync_DryRun_ShouldNotPersistEvidence()
        {
            // Arrange
            var metadata = new Dictionary<string, object?>
            {
                { "AssetName", "Test Token" },
                { "UnitName", "TEST" },
                { "Total", 1000000UL },
                { "Decimals", 6U }
            };

            var request = new ValidateTokenMetadataRequest
            {
                Context = new ValidationContext
                {
                    Network = "mainnet",
                    TokenStandard = "ASA"
                },
                TokenMetadata = metadata,
                DryRun = true
            };

            // Act
            var result = await _service.ValidateTokenMetadataAsync(request, TestUserAddress);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.EvidenceId, Is.Null);
            _repositoryMock.Verify(r => r.StoreValidationEvidenceAsync(It.IsAny<ValidationEvidence>()), Times.Never);
        }

        [Test]
        public async Task GetValidationEvidenceAsync_ExistingEvidence_ShouldReturn()
        {
            // Arrange
            var evidenceId = "test-evidence-123";
            var evidence = new ValidationEvidence
            {
                EvidenceId = evidenceId,
                Passed = true,
                Context = new ValidationContext
                {
                    Network = "mainnet",
                    TokenStandard = "ASA"
                }
            };

            _repositoryMock.Setup(r => r.GetValidationEvidenceByIdAsync(evidenceId))
                .ReturnsAsync(evidence);

            // Act
            var result = await _service.GetValidationEvidenceAsync(evidenceId);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Evidence, Is.Not.Null);
            Assert.That(result.Evidence.EvidenceId, Is.EqualTo(evidenceId));
        }

        [Test]
        public async Task GetValidationEvidenceAsync_NonExistentEvidence_ShouldReturnNotFound()
        {
            // Arrange
            var evidenceId = "non-existent-evidence";

            _repositoryMock.Setup(r => r.GetValidationEvidenceByIdAsync(evidenceId))
                .ReturnsAsync((ValidationEvidence?)null);

            // Act
            var result = await _service.GetValidationEvidenceAsync(evidenceId);

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.Evidence, Is.Null);
            Assert.That(result.ErrorMessage, Does.Contain("not found"));
        }

        #endregion

        #region Checksum Tests

        [Test]
        public void ComputeEvidenceChecksum_ShouldBeDeterministic()
        {
            // Arrange
            var evidence = new ValidationEvidence
            {
                EvidenceId = "test-123",
                Passed = true,
                Context = new ValidationContext
                {
                    Network = "mainnet",
                    TokenStandard = "ASA",
                    ValidatorVersion = "1.0.0",
                    RuleSetVersion = "1.0.0"
                },
                TotalRules = 6,
                PassedRules = 6,
                FailedRules = 0,
                SkippedRules = 0,
                ValidationTimestamp = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                RequestedBy = TestUserAddress
            };

            // Act
            var checksum1 = _service.ComputeEvidenceChecksum(evidence);
            var checksum2 = _service.ComputeEvidenceChecksum(evidence);

            // Assert
            Assert.That(checksum1, Is.EqualTo(checksum2));
            Assert.That(checksum1.Length, Is.EqualTo(64)); // SHA256 hex string length
        }

        [Test]
        public void ComputeEvidenceChecksum_DifferentData_ShouldDiffer()
        {
            // Arrange
            var evidence1 = new ValidationEvidence
            {
                EvidenceId = "test-123",
                Passed = true
            };

            var evidence2 = new ValidationEvidence
            {
                EvidenceId = "test-456",
                Passed = true
            };

            // Act
            var checksum1 = _service.ComputeEvidenceChecksum(evidence1);
            var checksum2 = _service.ComputeEvidenceChecksum(evidence2);

            // Assert
            Assert.That(checksum1, Is.Not.EqualTo(checksum2));
        }

        #endregion

        #region Validation Context Tests

        [Test]
        public async Task ValidateTokenMetadataAsync_WithComplianceFlags_ShouldIncludeInContext()
        {
            // Arrange
            var metadata = new Dictionary<string, object?>
            {
                { "AssetName", "Test Token" },
                { "UnitName", "TEST" },
                { "Total", 1000000UL },
                { "Decimals", 6U }
            };

            var request = new ValidateTokenMetadataRequest
            {
                Context = new ValidationContext
                {
                    Network = "voimain-v1.0",
                    TokenStandard = "ASA",
                    ComplianceFlags = new List<ComplianceFlag>
                    {
                        new ComplianceFlag
                        {
                            FlagId = "EU_MICA_ENABLED",
                            Description = "EU MICA compliance enabled",
                            Reason = "Token will be issued in EU markets"
                        }
                    },
                    JurisdictionToggles = new Dictionary<string, string>
                    {
                        { "EU", "MiCA profile enabled for EU compliance" }
                    }
                },
                TokenMetadata = metadata,
                DryRun = false
            };

            _repositoryMock.Setup(r => r.StoreValidationEvidenceAsync(It.IsAny<ValidationEvidence>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.ValidateTokenMetadataAsync(request, TestUserAddress);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Evidence!.Context.ComplianceFlags, Has.Count.EqualTo(1));
            Assert.That(result.Evidence.Context.ComplianceFlags[0].FlagId, Is.EqualTo("EU_MICA_ENABLED"));
            Assert.That(result.Evidence.Context.JurisdictionToggles, Does.ContainKey("EU"));
        }

        #endregion

        #region Unsupported Token Standard Tests

        [Test]
        public async Task ValidateTokenMetadataAsync_UnsupportedStandard_ShouldReturnError()
        {
            // Arrange
            var metadata = new Dictionary<string, object?>();

            var request = new ValidateTokenMetadataRequest
            {
                Context = new ValidationContext
                {
                    Network = "mainnet",
                    TokenStandard = "UNSUPPORTED"
                },
                TokenMetadata = metadata,
                DryRun = true
            };

            // Act
            var result = await _service.ValidateTokenMetadataAsync(request, TestUserAddress);

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("No validator found"));
        }

        #endregion
    }
}

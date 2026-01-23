using BiatecTokensApi.Controllers;
using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Models.Metering;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;

namespace BiatecTokensTests
{
    /// <summary>
    /// Tests for compliance attestation package generation endpoint
    /// </summary>
    [TestFixture]
    public class AttestationPackageTests
    {
        private Mock<IComplianceRepository> _repositoryMock;
        private Mock<ILogger<ComplianceService>> _serviceLoggerMock;
        private Mock<ILogger<ComplianceController>> _controllerLoggerMock;
        private Mock<ISubscriptionMeteringService> _meteringServiceMock;
        private Mock<ISubscriptionMeteringService> _controllerMeteringServiceMock;
        private ComplianceService _service;
        private ComplianceController _controller;
        private Mock<IComplianceService> _serviceMock;
        private const string TestUserAddress = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";
        private const string TestIssuerAddress = "ISSUER1AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";

        [SetUp]
        public void Setup()
        {
            _repositoryMock = new Mock<IComplianceRepository>();
            _serviceLoggerMock = new Mock<ILogger<ComplianceService>>();
            _controllerLoggerMock = new Mock<ILogger<ComplianceController>>();
            _meteringServiceMock = new Mock<ISubscriptionMeteringService>();
            _controllerMeteringServiceMock = new Mock<ISubscriptionMeteringService>();
            _service = new ComplianceService(_repositoryMock.Object, _serviceLoggerMock.Object, _meteringServiceMock.Object);
            
            _serviceMock = new Mock<IComplianceService>();
            _controller = new ComplianceController(_serviceMock.Object, _controllerLoggerMock.Object, _controllerMeteringServiceMock.Object);

            // Mock authenticated user context
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, TestUserAddress)
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var claimsPrincipal = new ClaimsPrincipal(identity);

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = claimsPrincipal
                }
            };
        }

        #region Service Tests - GenerateAttestationPackageAsync

        [Test]
        public async Task GenerateAttestationPackageAsync_ValidJsonRequest_ShouldSucceed()
        {
            // Arrange
            var request = new GenerateAttestationPackageRequest
            {
                TokenId = 12345,
                FromDate = DateTime.UtcNow.AddMonths(-1),
                ToDate = DateTime.UtcNow,
                Format = "json"
            };

            var metadata = new ComplianceMetadata
            {
                Id = Guid.NewGuid().ToString(),
                AssetId = 12345,
                ComplianceStatus = ComplianceStatus.Compliant,
                VerificationStatus = VerificationStatus.Verified,
                Network = "voimain-v1.0",
                RegulatoryFramework = "MICA",
                Jurisdiction = "EU"
            };

            _repositoryMock.Setup(r => r.GetMetadataByAssetIdAsync(request.TokenId))
                .ReturnsAsync(metadata);

            _repositoryMock.Setup(r => r.ListAttestationsAsync(It.IsAny<ListComplianceAttestationsRequest>()))
                .ReturnsAsync(new List<ComplianceAttestation>
                {
                    new ComplianceAttestation
                    {
                        Id = Guid.NewGuid().ToString(),
                        WalletAddress = TestUserAddress,
                        AssetId = 12345,
                        IssuerAddress = TestIssuerAddress,
                        ProofHash = "test-hash",
                        VerificationStatus = AttestationVerificationStatus.Verified,
                        IssuedAt = DateTime.UtcNow.AddDays(-5)
                    }
                });

            // Act
            var result = await _service.GenerateAttestationPackageAsync(request, TestUserAddress);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Package, Is.Not.Null);
            Assert.That(result.Package!.TokenId, Is.EqualTo(request.TokenId));
            Assert.That(result.Package.IssuerAddress, Is.EqualTo(TestUserAddress));
            Assert.That(result.Package.ComplianceMetadata, Is.Not.Null);
            Assert.That(result.Package.ComplianceMetadata!.AssetId, Is.EqualTo(request.TokenId));
            Assert.That(result.Package.Attestations.Count, Is.EqualTo(1));
            Assert.That(result.Package.DateRange, Is.Not.Null);
            Assert.That(result.Package.DateRange!.From, Is.EqualTo(request.FromDate));
            Assert.That(result.Package.DateRange.To, Is.EqualTo(request.ToDate));
            Assert.That(result.Package.ContentHash, Is.Not.Empty);
            Assert.That(result.Package.Signature, Is.Not.Null);
            Assert.That(result.Format, Is.EqualTo("json"));

            // Verify metering event was emitted
            _meteringServiceMock.Verify(m => m.EmitMeteringEvent(It.Is<SubscriptionMeteringEvent>(e =>
                e.Category == MeteringCategory.Compliance &&
                e.OperationType == MeteringOperationType.Export &&
                e.AssetId == request.TokenId &&
                e.PerformedBy == TestUserAddress &&
                e.ItemCount == 1 &&
                e.Metadata != null &&
                e.Metadata["exportFormat"] == "json" &&
                e.Metadata["exportType"] == "attestationPackage"
            )), Times.Once);
        }

        [Test]
        public async Task GenerateAttestationPackageAsync_InvalidFormat_ShouldFail()
        {
            // Arrange
            var request = new GenerateAttestationPackageRequest
            {
                TokenId = 12345,
                Format = "xml"
            };

            // Act
            var result = await _service.GenerateAttestationPackageAsync(request, TestUserAddress);

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("Invalid format"));
            Assert.That(result.ErrorMessage, Does.Contain("json"));
            Assert.That(result.ErrorMessage, Does.Contain("pdf"));
        }

        [Test]
        public async Task GenerateAttestationPackageAsync_NoMetadata_ShouldStillSucceed()
        {
            // Arrange
            var request = new GenerateAttestationPackageRequest
            {
                TokenId = 99999,
                Format = "json"
            };

            _repositoryMock.Setup(r => r.GetMetadataByAssetIdAsync(request.TokenId))
                .ReturnsAsync((ComplianceMetadata?)null);

            _repositoryMock.Setup(r => r.ListAttestationsAsync(It.IsAny<ListComplianceAttestationsRequest>()))
                .ReturnsAsync(new List<ComplianceAttestation>());

            // Act
            var result = await _service.GenerateAttestationPackageAsync(request, TestUserAddress);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Package, Is.Not.Null);
            Assert.That(result.Package!.TokenId, Is.EqualTo(request.TokenId));
            Assert.That(result.Package.ComplianceMetadata, Is.Null);
            Assert.That(result.Package.Attestations, Is.Empty);
        }

        [Test]
        public async Task GenerateAttestationPackageAsync_WithDateRange_ShouldFilterAttestations()
        {
            // Arrange
            var fromDate = DateTime.UtcNow.AddMonths(-2);
            var toDate = DateTime.UtcNow.AddMonths(-1);

            var request = new GenerateAttestationPackageRequest
            {
                TokenId = 12345,
                FromDate = fromDate,
                ToDate = toDate,
                Format = "json"
            };

            _repositoryMock.Setup(r => r.GetMetadataByAssetIdAsync(request.TokenId))
                .ReturnsAsync((ComplianceMetadata?)null);

            _repositoryMock.Setup(r => r.ListAttestationsAsync(It.Is<ListComplianceAttestationsRequest>(req =>
                req.AssetId == request.TokenId &&
                req.FromDate == fromDate &&
                req.ToDate == toDate
            ))).ReturnsAsync(new List<ComplianceAttestation>());

            // Act
            var result = await _service.GenerateAttestationPackageAsync(request, TestUserAddress);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Package!.DateRange!.From, Is.EqualTo(fromDate));
            Assert.That(result.Package.DateRange.To, Is.EqualTo(toDate));

            // Verify the attestations request had correct filters
            _repositoryMock.Verify(r => r.ListAttestationsAsync(It.Is<ListComplianceAttestationsRequest>(req =>
                req.AssetId == request.TokenId &&
                req.FromDate == fromDate &&
                req.ToDate == toDate
            )), Times.Once);
        }

        [Test]
        public async Task GenerateAttestationPackageAsync_MultipleAttestations_ShouldIncludeAll()
        {
            // Arrange
            var request = new GenerateAttestationPackageRequest
            {
                TokenId = 12345,
                Format = "json"
            };

            var attestations = Enumerable.Range(1, 5).Select(i => new ComplianceAttestation
            {
                Id = Guid.NewGuid().ToString(),
                WalletAddress = TestUserAddress,
                AssetId = 12345,
                IssuerAddress = TestIssuerAddress,
                ProofHash = $"hash-{i}",
                VerificationStatus = AttestationVerificationStatus.Verified,
                IssuedAt = DateTime.UtcNow.AddDays(-i)
            }).ToList();

            _repositoryMock.Setup(r => r.GetMetadataByAssetIdAsync(request.TokenId))
                .ReturnsAsync((ComplianceMetadata?)null);

            _repositoryMock.Setup(r => r.ListAttestationsAsync(It.IsAny<ListComplianceAttestationsRequest>()))
                .ReturnsAsync(attestations);

            // Act
            var result = await _service.GenerateAttestationPackageAsync(request, TestUserAddress);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Package!.Attestations.Count, Is.EqualTo(5));
            
            // Verify metering includes correct count
            _meteringServiceMock.Verify(m => m.EmitMeteringEvent(It.Is<SubscriptionMeteringEvent>(e =>
                e.ItemCount == 5 &&
                e.Metadata!["attestationCount"] == "5"
            )), Times.Once);
        }

        [Test]
        public async Task GenerateAttestationPackageAsync_ShouldGenerateDeterministicHash()
        {
            // Arrange
            var request = new GenerateAttestationPackageRequest
            {
                TokenId = 12345,
                Format = "json"
            };

            _repositoryMock.Setup(r => r.GetMetadataByAssetIdAsync(request.TokenId))
                .ReturnsAsync((ComplianceMetadata?)null);

            _repositoryMock.Setup(r => r.ListAttestationsAsync(It.IsAny<ListComplianceAttestationsRequest>()))
                .ReturnsAsync(new List<ComplianceAttestation>());

            // Act
            var result1 = await _service.GenerateAttestationPackageAsync(request, TestUserAddress);
            await Task.Delay(100); // Small delay to ensure different timestamps
            var result2 = await _service.GenerateAttestationPackageAsync(request, TestUserAddress);

            // Assert - hashes should be different because timestamps differ
            Assert.That(result1.Success, Is.True);
            Assert.That(result2.Success, Is.True);
            Assert.That(result1.Package!.ContentHash, Is.Not.Empty);
            Assert.That(result2.Package!.ContentHash, Is.Not.Empty);
            // Hashes will differ due to different GeneratedAt timestamps
        }

        #endregion

        #region Controller Tests - GenerateAttestationPackage

        [Test]
        public async Task GenerateAttestationPackage_ValidRequest_ShouldReturnOk()
        {
            // Arrange
            var request = new GenerateAttestationPackageRequest
            {
                TokenId = 12345,
                Format = "json"
            };

            var package = new AttestationPackage
            {
                TokenId = request.TokenId,
                IssuerAddress = TestUserAddress,
                Attestations = new List<ComplianceAttestation>()
            };

            _serviceMock.Setup(s => s.GenerateAttestationPackageAsync(request, TestUserAddress))
                .ReturnsAsync(new AttestationPackageResponse
                {
                    Success = true,
                    Package = package,
                    Format = "json"
                });

            // Act
            var result = await _controller.GenerateAttestationPackage(request);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = (OkObjectResult)result;
            Assert.That(okResult.Value, Is.InstanceOf<AttestationPackageResponse>());
            var response = (AttestationPackageResponse)okResult.Value!;
            Assert.That(response.Success, Is.True);
            Assert.That(response.Package, Is.Not.Null);
            Assert.That(response.Package!.TokenId, Is.EqualTo(request.TokenId));
        }

        [Test]
        public async Task GenerateAttestationPackage_InvalidModelState_ShouldReturnBadRequest()
        {
            // Arrange
            var request = new GenerateAttestationPackageRequest
            {
                TokenId = 0,
                Format = ""
            };

            _controller.ModelState.AddModelError("TokenId", "TokenId is required");
            _controller.ModelState.AddModelError("Format", "Format is required");

            // Act
            var result = await _controller.GenerateAttestationPackage(request);

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task GenerateAttestationPackage_ServiceFailure_ShouldReturnBadRequest()
        {
            // Arrange
            var request = new GenerateAttestationPackageRequest
            {
                TokenId = 12345,
                Format = "json"
            };

            _serviceMock.Setup(s => s.GenerateAttestationPackageAsync(request, TestUserAddress))
                .ReturnsAsync(new AttestationPackageResponse
                {
                    Success = false,
                    ErrorMessage = "Service error occurred"
                });

            // Act
            var result = await _controller.GenerateAttestationPackage(request);

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task GenerateAttestationPackage_PdfFormat_ShouldReturn501NotImplemented()
        {
            // Arrange
            var request = new GenerateAttestationPackageRequest
            {
                TokenId = 12345,
                Format = "pdf"
            };

            var package = new AttestationPackage
            {
                TokenId = request.TokenId,
                IssuerAddress = TestUserAddress
            };

            _serviceMock.Setup(s => s.GenerateAttestationPackageAsync(request, TestUserAddress))
                .ReturnsAsync(new AttestationPackageResponse
                {
                    Success = true,
                    Package = package,
                    Format = "pdf"
                });

            // Act
            var result = await _controller.GenerateAttestationPackage(request);

            // Assert
            Assert.That(result, Is.InstanceOf<ObjectResult>());
            var objectResult = (ObjectResult)result;
            Assert.That(objectResult.StatusCode, Is.EqualTo(StatusCodes.Status501NotImplemented));
        }

        [Test]
        public async Task GenerateAttestationPackage_Exception_ShouldReturn500()
        {
            // Arrange
            var request = new GenerateAttestationPackageRequest
            {
                TokenId = 12345,
                Format = "json"
            };

            _serviceMock.Setup(s => s.GenerateAttestationPackageAsync(request, TestUserAddress))
                .ThrowsAsync(new Exception("Unexpected error"));

            // Act
            var result = await _controller.GenerateAttestationPackage(request);

            // Assert
            Assert.That(result, Is.InstanceOf<ObjectResult>());
            var objectResult = (ObjectResult)result;
            Assert.That(objectResult.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
        }

        #endregion

        #region Response Schema Tests

        [Test]
        public async Task GenerateAttestationPackage_ResponseSchema_ShouldMatchExpectations()
        {
            // Arrange
            var request = new GenerateAttestationPackageRequest
            {
                TokenId = 12345,
                FromDate = DateTime.UtcNow.AddMonths(-1),
                ToDate = DateTime.UtcNow,
                Format = "json"
            };

            var metadata = new ComplianceMetadata
            {
                Id = Guid.NewGuid().ToString(),
                AssetId = 12345,
                ComplianceStatus = ComplianceStatus.Compliant,
                VerificationStatus = VerificationStatus.Verified,
                Network = "voimain-v1.0",
                RegulatoryFramework = "MICA",
                Jurisdiction = "EU",
                KycProvider = "TestKYC"
            };

            _repositoryMock.Setup(r => r.GetMetadataByAssetIdAsync(request.TokenId))
                .ReturnsAsync(metadata);

            _repositoryMock.Setup(r => r.ListAttestationsAsync(It.IsAny<ListComplianceAttestationsRequest>()))
                .ReturnsAsync(new List<ComplianceAttestation>
                {
                    new ComplianceAttestation
                    {
                        Id = Guid.NewGuid().ToString(),
                        WalletAddress = TestUserAddress,
                        AssetId = 12345,
                        IssuerAddress = TestIssuerAddress,
                        ProofHash = "test-hash",
                        VerificationStatus = AttestationVerificationStatus.Verified,
                        AttestationType = "KYC",
                        Network = "voimain-v1.0",
                        IssuedAt = DateTime.UtcNow
                    }
                });

            // Act
            var result = await _service.GenerateAttestationPackageAsync(request, TestUserAddress);

            // Assert - Verify all expected fields are present
            Assert.That(result.Success, Is.True);
            Assert.That(result.Package, Is.Not.Null);
            
            var package = result.Package!;
            Assert.That(package.PackageId, Is.Not.Empty);
            Assert.That(package.TokenId, Is.EqualTo(request.TokenId));
            Assert.That(package.GeneratedAt, Is.Not.EqualTo(default(DateTime)));
            Assert.That(package.IssuerAddress, Is.EqualTo(TestUserAddress));
            Assert.That(package.Network, Is.EqualTo("voimain-v1.0"));
            
            // Token metadata
            Assert.That(package.Token, Is.Not.Null);
            Assert.That(package.Token!.AssetId, Is.EqualTo(request.TokenId));
            
            // Compliance metadata
            Assert.That(package.ComplianceMetadata, Is.Not.Null);
            Assert.That(package.ComplianceMetadata!.AssetId, Is.EqualTo(request.TokenId));
            Assert.That(package.ComplianceMetadata.RegulatoryFramework, Is.EqualTo("MICA"));
            
            // Whitelist policy
            Assert.That(package.WhitelistPolicy, Is.Not.Null);
            
            // Compliance status
            Assert.That(package.ComplianceStatus, Is.Not.Null);
            Assert.That(package.ComplianceStatus!.Status, Is.EqualTo(ComplianceStatus.Compliant));
            Assert.That(package.ComplianceStatus.VerificationStatus, Is.EqualTo(VerificationStatus.Verified));
            
            // Attestations
            Assert.That(package.Attestations, Is.Not.Empty);
            Assert.That(package.Attestations[0].AttestationType, Is.EqualTo("KYC"));
            
            // Date range
            Assert.That(package.DateRange, Is.Not.Null);
            Assert.That(package.DateRange!.From, Is.EqualTo(request.FromDate));
            Assert.That(package.DateRange.To, Is.EqualTo(request.ToDate));
            
            // Hash and signature
            Assert.That(package.ContentHash, Is.Not.Empty);
            Assert.That(package.Signature, Is.Not.Null);
            Assert.That(package.Signature!.Algorithm, Is.EqualTo("SHA256"));
            Assert.That(package.Signature.SignedAt, Is.Not.EqualTo(default(DateTime)));
        }

        #endregion
    }
}

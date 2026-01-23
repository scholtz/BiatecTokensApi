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
    [TestFixture]
    public class ComplianceAttestationTests
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
            var whitelistServiceMock = new Mock<IWhitelistService>();
            _service = new ComplianceService(_repositoryMock.Object, whitelistServiceMock.Object, _serviceLoggerMock.Object, _meteringServiceMock.Object);
            
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

        #region Service Tests - CreateAttestationAsync

        [Test]
        public async Task CreateAttestationAsync_ValidRequest_ShouldSucceed()
        {
            // Arrange
            var request = new CreateComplianceAttestationRequest
            {
                WalletAddress = TestUserAddress,
                AssetId = 12345,
                IssuerAddress = TestIssuerAddress,
                ProofHash = "QmYwAPJzv5CZsnA625s3Xf2nemtYgPpHdWEz79ojWnPbdG",
                ProofType = "IPFS",
                AttestationType = "KYC",
                Network = "voimain-v1.0",
                Jurisdiction = "US,EU",
                RegulatoryFramework = "MICA"
            };

            _repositoryMock.Setup(r => r.CreateAttestationAsync(It.IsAny<ComplianceAttestation>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.CreateAttestationAsync(request, TestUserAddress);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Attestation, Is.Not.Null);
            Assert.That(result.Attestation!.WalletAddress, Is.EqualTo(request.WalletAddress));
            Assert.That(result.Attestation.AssetId, Is.EqualTo(request.AssetId));
            Assert.That(result.Attestation.IssuerAddress, Is.EqualTo(request.IssuerAddress));
            Assert.That(result.Attestation.ProofHash, Is.EqualTo(request.ProofHash));
            Assert.That(result.Attestation.ProofType, Is.EqualTo(request.ProofType));
            Assert.That(result.Attestation.AttestationType, Is.EqualTo(request.AttestationType));
            Assert.That(result.Attestation.VerificationStatus, Is.EqualTo(AttestationVerificationStatus.Pending));
            Assert.That(result.Attestation.CreatedBy, Is.EqualTo(TestUserAddress));

            // Verify metering event was emitted
            _meteringServiceMock.Verify(m => m.EmitMeteringEvent(It.Is<SubscriptionMeteringEvent>(e =>
                e.Category == MeteringCategory.Compliance &&
                e.OperationType == MeteringOperationType.Add &&
                e.AssetId == request.AssetId &&
                e.ItemCount == 1
            )), Times.Once);
        }

        [Test]
        public async Task CreateAttestationAsync_MissingWalletAddress_ShouldFail()
        {
            // Arrange
            var request = new CreateComplianceAttestationRequest
            {
                WalletAddress = "",
                AssetId = 12345,
                IssuerAddress = TestIssuerAddress,
                ProofHash = "QmYwAPJzv5CZsnA625s3Xf2nemtYgPpHdWEz79ojWnPbdG"
            };

            // Act
            var result = await _service.CreateAttestationAsync(request, TestUserAddress);

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("Wallet address"));
        }

        [Test]
        public async Task CreateAttestationAsync_MissingIssuerAddress_ShouldFail()
        {
            // Arrange
            var request = new CreateComplianceAttestationRequest
            {
                WalletAddress = TestUserAddress,
                AssetId = 12345,
                IssuerAddress = "",
                ProofHash = "QmYwAPJzv5CZsnA625s3Xf2nemtYgPpHdWEz79ojWnPbdG"
            };

            // Act
            var result = await _service.CreateAttestationAsync(request, TestUserAddress);

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("Issuer address"));
        }

        [Test]
        public async Task CreateAttestationAsync_MissingProofHash_ShouldFail()
        {
            // Arrange
            var request = new CreateComplianceAttestationRequest
            {
                WalletAddress = TestUserAddress,
                AssetId = 12345,
                IssuerAddress = TestIssuerAddress,
                ProofHash = ""
            };

            // Act
            var result = await _service.CreateAttestationAsync(request, TestUserAddress);

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("Proof hash"));
        }

        [Test]
        public async Task CreateAttestationAsync_RepositoryFailure_ShouldFail()
        {
            // Arrange
            var request = new CreateComplianceAttestationRequest
            {
                WalletAddress = TestUserAddress,
                AssetId = 12345,
                IssuerAddress = TestIssuerAddress,
                ProofHash = "QmYwAPJzv5CZsnA625s3Xf2nemtYgPpHdWEz79ojWnPbdG"
            };

            _repositoryMock.Setup(r => r.CreateAttestationAsync(It.IsAny<ComplianceAttestation>()))
                .ReturnsAsync(false);

            // Act
            var result = await _service.CreateAttestationAsync(request, TestUserAddress);

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("Failed to create attestation"));
        }

        #endregion

        #region Service Tests - GetAttestationAsync

        [Test]
        public async Task GetAttestationAsync_ExistingAttestation_ShouldSucceed()
        {
            // Arrange
            var attestationId = Guid.NewGuid().ToString();
            var attestation = new ComplianceAttestation
            {
                Id = attestationId,
                WalletAddress = TestUserAddress,
                AssetId = 12345,
                IssuerAddress = TestIssuerAddress,
                ProofHash = "QmYwAPJzv5CZsnA625s3Xf2nemtYgPpHdWEz79ojWnPbdG",
                VerificationStatus = AttestationVerificationStatus.Verified
            };

            _repositoryMock.Setup(r => r.GetAttestationByIdAsync(attestationId))
                .ReturnsAsync(attestation);

            // Act
            var result = await _service.GetAttestationAsync(attestationId);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Attestation, Is.Not.Null);
            Assert.That(result.Attestation!.Id, Is.EqualTo(attestationId));
        }

        [Test]
        public async Task GetAttestationAsync_NonExistingAttestation_ShouldFail()
        {
            // Arrange
            var attestationId = Guid.NewGuid().ToString();
            _repositoryMock.Setup(r => r.GetAttestationByIdAsync(attestationId))
                .ReturnsAsync((ComplianceAttestation?)null);

            // Act
            var result = await _service.GetAttestationAsync(attestationId);

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("not found"));
        }

        [Test]
        public async Task GetAttestationAsync_ExpiredAttestation_ShouldMarkAsExpired()
        {
            // Arrange
            var attestationId = Guid.NewGuid().ToString();
            var attestation = new ComplianceAttestation
            {
                Id = attestationId,
                WalletAddress = TestUserAddress,
                AssetId = 12345,
                IssuerAddress = TestIssuerAddress,
                ProofHash = "QmYwAPJzv5CZsnA625s3Xf2nemtYgPpHdWEz79ojWnPbdG",
                VerificationStatus = AttestationVerificationStatus.Verified,
                ExpiresAt = DateTime.UtcNow.AddDays(-1) // Expired yesterday
            };

            _repositoryMock.Setup(r => r.GetAttestationByIdAsync(attestationId))
                .ReturnsAsync(attestation);

            // Act
            var result = await _service.GetAttestationAsync(attestationId);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Attestation!.VerificationStatus, Is.EqualTo(AttestationVerificationStatus.Expired));
        }

        #endregion

        #region Service Tests - ListAttestationsAsync

        [Test]
        public async Task ListAttestationsAsync_NoFilters_ShouldReturnAll()
        {
            // Arrange
            var attestations = new List<ComplianceAttestation>
            {
                new ComplianceAttestation
                {
                    Id = Guid.NewGuid().ToString(),
                    WalletAddress = TestUserAddress,
                    AssetId = 12345,
                    IssuerAddress = TestIssuerAddress,
                    ProofHash = "hash1"
                },
                new ComplianceAttestation
                {
                    Id = Guid.NewGuid().ToString(),
                    WalletAddress = TestUserAddress,
                    AssetId = 67890,
                    IssuerAddress = TestIssuerAddress,
                    ProofHash = "hash2"
                }
            };

            var request = new ListComplianceAttestationsRequest();

            _repositoryMock.Setup(r => r.ListAttestationsAsync(request))
                .ReturnsAsync(attestations);
            _repositoryMock.Setup(r => r.GetAttestationCountAsync(request))
                .ReturnsAsync(attestations.Count);

            // Act
            var result = await _service.ListAttestationsAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Attestations.Count, Is.EqualTo(2));
            Assert.That(result.TotalCount, Is.EqualTo(2));
            Assert.That(result.Page, Is.EqualTo(1));
            Assert.That(result.TotalPages, Is.EqualTo(1));
        }

        [Test]
        public async Task ListAttestationsAsync_FilterByWalletAddress_ShouldReturnMatching()
        {
            // Arrange
            var attestations = new List<ComplianceAttestation>
            {
                new ComplianceAttestation
                {
                    Id = Guid.NewGuid().ToString(),
                    WalletAddress = TestUserAddress,
                    AssetId = 12345,
                    IssuerAddress = TestIssuerAddress,
                    ProofHash = "hash1"
                }
            };

            var request = new ListComplianceAttestationsRequest
            {
                WalletAddress = TestUserAddress
            };

            _repositoryMock.Setup(r => r.ListAttestationsAsync(request))
                .ReturnsAsync(attestations);
            _repositoryMock.Setup(r => r.GetAttestationCountAsync(request))
                .ReturnsAsync(attestations.Count);

            // Act
            var result = await _service.ListAttestationsAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Attestations.Count, Is.EqualTo(1));
            Assert.That(result.Attestations[0].WalletAddress, Is.EqualTo(TestUserAddress));
        }

        [Test]
        public async Task ListAttestationsAsync_FilterByAssetId_ShouldReturnMatching()
        {
            // Arrange
            var assetId = 12345ul;
            var attestations = new List<ComplianceAttestation>
            {
                new ComplianceAttestation
                {
                    Id = Guid.NewGuid().ToString(),
                    WalletAddress = TestUserAddress,
                    AssetId = assetId,
                    IssuerAddress = TestIssuerAddress,
                    ProofHash = "hash1"
                }
            };

            var request = new ListComplianceAttestationsRequest
            {
                AssetId = assetId
            };

            _repositoryMock.Setup(r => r.ListAttestationsAsync(request))
                .ReturnsAsync(attestations);
            _repositoryMock.Setup(r => r.GetAttestationCountAsync(request))
                .ReturnsAsync(attestations.Count);

            // Act
            var result = await _service.ListAttestationsAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Attestations.Count, Is.EqualTo(1));
            Assert.That(result.Attestations[0].AssetId, Is.EqualTo(assetId));
        }

        [Test]
        public async Task ListAttestationsAsync_WithPagination_ShouldCalculateTotalPages()
        {
            // Arrange
            var attestations = new List<ComplianceAttestation>
            {
                new ComplianceAttestation { Id = Guid.NewGuid().ToString(), WalletAddress = TestUserAddress, AssetId = 1, IssuerAddress = TestIssuerAddress, ProofHash = "h1" },
                new ComplianceAttestation { Id = Guid.NewGuid().ToString(), WalletAddress = TestUserAddress, AssetId = 2, IssuerAddress = TestIssuerAddress, ProofHash = "h2" }
            };

            var request = new ListComplianceAttestationsRequest
            {
                Page = 1,
                PageSize = 2
            };

            _repositoryMock.Setup(r => r.ListAttestationsAsync(request))
                .ReturnsAsync(attestations);
            _repositoryMock.Setup(r => r.GetAttestationCountAsync(request))
                .ReturnsAsync(5); // Total of 5 items

            // Act
            var result = await _service.ListAttestationsAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.TotalCount, Is.EqualTo(5));
            Assert.That(result.TotalPages, Is.EqualTo(3)); // 5 items / 2 per page = 3 pages
        }

        #endregion

        #region Controller Tests

        [Test]
        public async Task CreateAttestation_ValidRequest_ShouldReturnOk()
        {
            // Arrange
            var request = new CreateComplianceAttestationRequest
            {
                WalletAddress = TestUserAddress,
                AssetId = 12345,
                IssuerAddress = TestIssuerAddress,
                ProofHash = "QmYwAPJzv5CZsnA625s3Xf2nemtYgPpHdWEz79ojWnPbdG"
            };

            var attestation = new ComplianceAttestation
            {
                Id = Guid.NewGuid().ToString(),
                WalletAddress = request.WalletAddress,
                AssetId = request.AssetId,
                IssuerAddress = request.IssuerAddress,
                ProofHash = request.ProofHash
            };

            _serviceMock.Setup(s => s.CreateAttestationAsync(request, TestUserAddress))
                .ReturnsAsync(new ComplianceAttestationResponse
                {
                    Success = true,
                    Attestation = attestation
                });

            // Act
            var result = await _controller.CreateAttestation(request);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            Assert.That(okResult!.Value, Is.InstanceOf<ComplianceAttestationResponse>());
            var response = okResult.Value as ComplianceAttestationResponse;
            Assert.That(response!.Success, Is.True);
            Assert.That(response.Attestation, Is.Not.Null);
        }

        [Test]
        public async Task CreateAttestation_InvalidRequest_ShouldReturnBadRequest()
        {
            // Arrange
            var request = new CreateComplianceAttestationRequest
            {
                WalletAddress = TestUserAddress,
                AssetId = 12345,
                IssuerAddress = TestIssuerAddress,
                ProofHash = "QmYwAPJzv5CZsnA625s3Xf2nemtYgPpHdWEz79ojWnPbdG"
            };

            _serviceMock.Setup(s => s.CreateAttestationAsync(request, TestUserAddress))
                .ReturnsAsync(new ComplianceAttestationResponse
                {
                    Success = false,
                    ErrorMessage = "Validation error"
                });

            // Act
            var result = await _controller.CreateAttestation(request);

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task GetAttestation_ExistingAttestation_ShouldReturnOk()
        {
            // Arrange
            var attestationId = Guid.NewGuid().ToString();
            var attestation = new ComplianceAttestation
            {
                Id = attestationId,
                WalletAddress = TestUserAddress,
                AssetId = 12345,
                IssuerAddress = TestIssuerAddress,
                ProofHash = "QmYwAPJzv5CZsnA625s3Xf2nemtYgPpHdWEz79ojWnPbdG"
            };

            _serviceMock.Setup(s => s.GetAttestationAsync(attestationId))
                .ReturnsAsync(new ComplianceAttestationResponse
                {
                    Success = true,
                    Attestation = attestation
                });

            // Act
            var result = await _controller.GetAttestation(attestationId);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            var response = okResult!.Value as ComplianceAttestationResponse;
            Assert.That(response!.Success, Is.True);
            Assert.That(response.Attestation!.Id, Is.EqualTo(attestationId));
        }

        [Test]
        public async Task GetAttestation_NonExistingAttestation_ShouldReturnNotFound()
        {
            // Arrange
            var attestationId = Guid.NewGuid().ToString();
            _serviceMock.Setup(s => s.GetAttestationAsync(attestationId))
                .ReturnsAsync(new ComplianceAttestationResponse
                {
                    Success = false,
                    ErrorMessage = "Attestation not found"
                });

            // Act
            var result = await _controller.GetAttestation(attestationId);

            // Assert
            Assert.That(result, Is.InstanceOf<NotFoundObjectResult>());
        }

        [Test]
        public async Task ListAttestations_ValidRequest_ShouldReturnOk()
        {
            // Arrange
            var attestations = new List<ComplianceAttestation>
            {
                new ComplianceAttestation { Id = Guid.NewGuid().ToString(), WalletAddress = TestUserAddress, AssetId = 12345, IssuerAddress = TestIssuerAddress, ProofHash = "h1" }
            };

            _serviceMock.Setup(s => s.ListAttestationsAsync(It.IsAny<ListComplianceAttestationsRequest>()))
                .ReturnsAsync(new ComplianceAttestationListResponse
                {
                    Success = true,
                    Attestations = attestations,
                    TotalCount = 1,
                    Page = 1,
                    PageSize = 20,
                    TotalPages = 1
                });

            // Act
            var result = await _controller.ListAttestations();

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            var response = okResult!.Value as ComplianceAttestationListResponse;
            Assert.That(response!.Success, Is.True);
            Assert.That(response.Attestations.Count, Is.EqualTo(1));
        }

        [Test]
        public async Task ListAttestations_WithFilters_ShouldPassFiltersToService()
        {
            // Arrange
            var walletAddress = TestUserAddress;
            var assetId = 12345ul;
            var verificationStatus = AttestationVerificationStatus.Verified;

            _serviceMock.Setup(s => s.ListAttestationsAsync(It.Is<ListComplianceAttestationsRequest>(r =>
                r.WalletAddress == walletAddress &&
                r.AssetId == assetId &&
                r.VerificationStatus == verificationStatus
            )))
                .ReturnsAsync(new ComplianceAttestationListResponse
                {
                    Success = true,
                    Attestations = new List<ComplianceAttestation>(),
                    TotalCount = 0,
                    Page = 1,
                    PageSize = 20,
                    TotalPages = 0
                });

            // Act
            var result = await _controller.ListAttestations(
                walletAddress: walletAddress,
                assetId: assetId,
                verificationStatus: verificationStatus
            );

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            _serviceMock.Verify(s => s.ListAttestationsAsync(It.Is<ListComplianceAttestationsRequest>(r =>
                r.WalletAddress == walletAddress &&
                r.AssetId == assetId &&
                r.VerificationStatus == verificationStatus
            )), Times.Once);
        }

        #endregion

        #region Controller Tests - Export Endpoints

        [Test]
        public async Task ExportAttestationsJson_WithFilters_ShouldReturnJsonFile()
        {
            // Arrange
            var attestations = new List<ComplianceAttestation>
            {
                new ComplianceAttestation
                {
                    Id = "test-id-1",
                    WalletAddress = TestUserAddress,
                    AssetId = 12345,
                    IssuerAddress = TestIssuerAddress,
                    ProofHash = "QmYwAPJzv5CZsnA625s3Xf2nemtYgPpHdWEz79ojWnPbdG",
                    ProofType = "IPFS",
                    AttestationType = "KYC",
                    Network = "voimain-v1.0",
                    IssuedAt = DateTime.UtcNow.AddDays(-10),
                    VerificationStatus = AttestationVerificationStatus.Verified
                },
                new ComplianceAttestation
                {
                    Id = "test-id-2",
                    WalletAddress = TestUserAddress,
                    AssetId = 12345,
                    IssuerAddress = TestIssuerAddress,
                    ProofHash = "QmYwAPJzv5CZsnA625s3Xf2nemtYgPpHdWEz79ojWnPbdH",
                    ProofType = "IPFS",
                    AttestationType = "AML",
                    Network = "voimain-v1.0",
                    IssuedAt = DateTime.UtcNow.AddDays(-5),
                    VerificationStatus = AttestationVerificationStatus.Verified
                }
            };

            _serviceMock.Setup(s => s.ListAttestationsAsync(It.IsAny<ListComplianceAttestationsRequest>()))
                .ReturnsAsync(new ComplianceAttestationListResponse
                {
                    Success = true,
                    Attestations = attestations,
                    TotalCount = attestations.Count,
                    Page = 1,
                    PageSize = 100
                });

            // Act
            var result = await _controller.ExportAttestationsJson(
                walletAddress: TestUserAddress,
                assetId: 12345,
                fromDate: DateTime.UtcNow.AddDays(-30),
                toDate: DateTime.UtcNow
            );

            // Assert
            Assert.That(result, Is.InstanceOf<FileContentResult>());
            var fileResult = result as FileContentResult;
            Assert.That(fileResult!.ContentType, Is.EqualTo("application/json"));
            Assert.That(fileResult.FileDownloadName, Does.StartWith("attestations-export-"));
            Assert.That(fileResult.FileDownloadName, Does.EndWith(".json"));
            
            // Verify service was called with correct filters
            _serviceMock.Verify(s => s.ListAttestationsAsync(It.Is<ListComplianceAttestationsRequest>(r =>
                r.WalletAddress == TestUserAddress &&
                r.AssetId == 12345 &&
                r.FromDate.HasValue &&
                r.ToDate.HasValue
            )), Times.Once);
        }

        [Test]
        public async Task ExportAttestationsCsv_WithFilters_ShouldReturnCsvFile()
        {
            // Arrange
            var attestations = new List<ComplianceAttestation>
            {
                new ComplianceAttestation
                {
                    Id = "test-id-1",
                    WalletAddress = TestUserAddress,
                    AssetId = 12345,
                    IssuerAddress = TestIssuerAddress,
                    ProofHash = "QmYwAPJzv5CZsnA625s3Xf2nemtYgPpHdWEz79ojWnPbdG",
                    ProofType = "IPFS",
                    AttestationType = "KYC",
                    Network = "voimain-v1.0",
                    Jurisdiction = "US,EU",
                    RegulatoryFramework = "MICA",
                    IssuedAt = DateTime.UtcNow.AddDays(-10),
                    VerificationStatus = AttestationVerificationStatus.Verified,
                    CreatedBy = TestUserAddress,
                    CreatedAt = DateTime.UtcNow.AddDays(-10)
                }
            };

            _serviceMock.Setup(s => s.ListAttestationsAsync(It.IsAny<ListComplianceAttestationsRequest>()))
                .ReturnsAsync(new ComplianceAttestationListResponse
                {
                    Success = true,
                    Attestations = attestations,
                    TotalCount = attestations.Count,
                    Page = 1,
                    PageSize = 100
                });

            // Act
            var result = await _controller.ExportAttestationsCsv(
                assetId: 12345,
                network: "voimain-v1.0"
            );

            // Assert
            Assert.That(result, Is.InstanceOf<FileContentResult>());
            var fileResult = result as FileContentResult;
            Assert.That(fileResult!.ContentType, Is.EqualTo("text/csv"));
            Assert.That(fileResult.FileDownloadName, Does.StartWith("attestations-export-"));
            Assert.That(fileResult.FileDownloadName, Does.EndWith(".csv"));
            
            // Verify CSV contains headers
            var csvContent = System.Text.Encoding.UTF8.GetString(fileResult.FileContents);
            Assert.That(csvContent, Does.Contain("Id,WalletAddress,AssetId,IssuerAddress"));
            Assert.That(csvContent, Does.Contain("test-id-1"));
            Assert.That(csvContent, Does.Contain(TestUserAddress));
            Assert.That(csvContent, Does.Contain("12345"));
        }

        [Test]
        public async Task ExportAttestationsJson_WithDateRangeFilter_ShouldPassFiltersToService()
        {
            // Arrange
            var fromDate = DateTime.UtcNow.AddDays(-30);
            var toDate = DateTime.UtcNow;

            _serviceMock.Setup(s => s.ListAttestationsAsync(It.IsAny<ListComplianceAttestationsRequest>()))
                .ReturnsAsync(new ComplianceAttestationListResponse
                {
                    Success = true,
                    Attestations = new List<ComplianceAttestation>(),
                    TotalCount = 0
                });

            // Act
            var result = await _controller.ExportAttestationsJson(
                fromDate: fromDate,
                toDate: toDate,
                attestationType: "KYC"
            );

            // Assert
            Assert.That(result, Is.InstanceOf<FileContentResult>());
            _serviceMock.Verify(s => s.ListAttestationsAsync(It.Is<ListComplianceAttestationsRequest>(r =>
                r.FromDate == fromDate &&
                r.ToDate == toDate &&
                r.AttestationType == "KYC"
            )), Times.Once);
        }

        [Test]
        public async Task ExportAttestationsCsv_ServiceFailure_ShouldReturn500()
        {
            // Arrange
            _serviceMock.Setup(s => s.ListAttestationsAsync(It.IsAny<ListComplianceAttestationsRequest>()))
                .ReturnsAsync(new ComplianceAttestationListResponse
                {
                    Success = false,
                    ErrorMessage = "Database error"
                });

            // Act
            var result = await _controller.ExportAttestationsCsv();

            // Assert
            Assert.That(result, Is.InstanceOf<ObjectResult>());
            var objectResult = result as ObjectResult;
            Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
        }

        [Test]
        public async Task ExportAttestationsJson_WithPagination_ShouldLimitPageSize()
        {
            // Arrange
            _serviceMock.Setup(s => s.ListAttestationsAsync(It.IsAny<ListComplianceAttestationsRequest>()))
                .ReturnsAsync(new ComplianceAttestationListResponse
                {
                    Success = true,
                    Attestations = new List<ComplianceAttestation>(),
                    TotalCount = 0
                });

            // Act - Request page size larger than max
            var result = await _controller.ExportAttestationsJson(pageSize: 20000);

            // Assert
            Assert.That(result, Is.InstanceOf<FileContentResult>());
            _serviceMock.Verify(s => s.ListAttestationsAsync(It.Is<ListComplianceAttestationsRequest>(r =>
                r.PageSize == 10000 // Should be capped at MaxExportRecords
            )), Times.Once);
        }

        [Test]
        public async Task ExportAttestationsCsv_WithSpecialCharacters_ShouldEscapeCorrectly()
        {
            // Arrange
            var attestations = new List<ComplianceAttestation>
            {
                new ComplianceAttestation
                {
                    Id = "test-id-1",
                    WalletAddress = TestUserAddress,
                    AssetId = 12345,
                    IssuerAddress = TestIssuerAddress,
                    ProofHash = "Hash\"WithQuotes",
                    Notes = "Note with \"quotes\" and, commas",
                    AttestationType = "KYC",
                    VerificationStatus = AttestationVerificationStatus.Verified,
                    IssuedAt = DateTime.UtcNow,
                    CreatedBy = TestUserAddress,
                    CreatedAt = DateTime.UtcNow
                }
            };

            _serviceMock.Setup(s => s.ListAttestationsAsync(It.IsAny<ListComplianceAttestationsRequest>()))
                .ReturnsAsync(new ComplianceAttestationListResponse
                {
                    Success = true,
                    Attestations = attestations,
                    TotalCount = 1
                });

            // Act
            var result = await _controller.ExportAttestationsCsv();

            // Assert
            Assert.That(result, Is.InstanceOf<FileContentResult>());
            var fileResult = result as FileContentResult;
            var csvContent = System.Text.Encoding.UTF8.GetString(fileResult!.FileContents);
            
            // Verify quotes are escaped (doubled)
            Assert.That(csvContent, Does.Contain("Hash\"\"WithQuotes"));
            Assert.That(csvContent, Does.Contain("Note with \"\"quotes\"\" and, commas"));
        }

        #endregion
    }
}

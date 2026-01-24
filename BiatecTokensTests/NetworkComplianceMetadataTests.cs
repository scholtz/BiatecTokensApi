using BiatecTokensApi.Controllers;
using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;

namespace BiatecTokensTests
{
    [TestFixture]
    public class NetworkComplianceMetadataTests
    {
        private Mock<IComplianceService> _serviceMock;
        private Mock<ILogger<ComplianceController>> _loggerMock;
        private Mock<ISubscriptionMeteringService> _meteringServiceMock;
        private ComplianceController _controller;
        private const string TestUserAddress = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";

        [SetUp]
        public void Setup()
        {
            _serviceMock = new Mock<IComplianceService>();
            _loggerMock = new Mock<ILogger<ComplianceController>>();
            _meteringServiceMock = new Mock<ISubscriptionMeteringService>();
            _controller = new ComplianceController(_serviceMock.Object, _loggerMock.Object, _meteringServiceMock.Object);

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

        [Test]
        public async Task GetNetworkComplianceMetadata_ShouldReturnNetworkList()
        {
            // Arrange
            var expectedNetworks = new List<NetworkComplianceMetadata>
            {
                new NetworkComplianceMetadata
                {
                    Network = "voimain-v1.0",
                    NetworkName = "VOI Mainnet",
                    IsMicaReady = true,
                    RequiresWhitelisting = true,
                    RequiresJurisdiction = true,
                    RequiresRegulatoryFramework = false
                },
                new NetworkComplianceMetadata
                {
                    Network = "aramidmain-v1.0",
                    NetworkName = "Aramid Mainnet",
                    IsMicaReady = true,
                    RequiresWhitelisting = true,
                    RequiresJurisdiction = false,
                    RequiresRegulatoryFramework = true
                }
            };

            _serviceMock.Setup(s => s.GetNetworkComplianceMetadataAsync())
                .ReturnsAsync(new NetworkComplianceMetadataResponse
                {
                    Success = true,
                    Networks = expectedNetworks,
                    CacheDurationSeconds = 3600
                });

            // Act
            var result = await _controller.GetNetworkComplianceMetadata();

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            Assert.That(okResult!.Value, Is.InstanceOf<NetworkComplianceMetadataResponse>());
            var response = okResult.Value as NetworkComplianceMetadataResponse;
            Assert.That(response!.Success, Is.True);
            Assert.That(response.Networks, Has.Count.EqualTo(2));
        }

        [Test]
        public async Task GetNetworkComplianceMetadata_ShouldIncludeVOINetwork()
        {
            // Arrange
            var expectedNetworks = new List<NetworkComplianceMetadata>
            {
                new NetworkComplianceMetadata
                {
                    Network = "voimain-v1.0",
                    NetworkName = "VOI Mainnet",
                    IsMicaReady = true,
                    RequiresWhitelisting = true,
                    RequiresJurisdiction = true
                }
            };

            _serviceMock.Setup(s => s.GetNetworkComplianceMetadataAsync())
                .ReturnsAsync(new NetworkComplianceMetadataResponse
                {
                    Success = true,
                    Networks = expectedNetworks
                });

            // Act
            var result = await _controller.GetNetworkComplianceMetadata();

            // Assert
            var okResult = result as OkObjectResult;
            var response = okResult!.Value as NetworkComplianceMetadataResponse;
            var voiNetwork = response!.Networks.FirstOrDefault(n => n.Network == "voimain-v1.0");
            
            Assert.That(voiNetwork, Is.Not.Null);
            Assert.That(voiNetwork!.IsMicaReady, Is.True);
            Assert.That(voiNetwork.RequiresWhitelisting, Is.True);
            Assert.That(voiNetwork.RequiresJurisdiction, Is.True);
        }

        [Test]
        public async Task GetNetworkComplianceMetadata_ShouldIncludeAramidNetwork()
        {
            // Arrange
            var expectedNetworks = new List<NetworkComplianceMetadata>
            {
                new NetworkComplianceMetadata
                {
                    Network = "aramidmain-v1.0",
                    NetworkName = "Aramid Mainnet",
                    IsMicaReady = true,
                    RequiresWhitelisting = true,
                    RequiresRegulatoryFramework = true
                }
            };

            _serviceMock.Setup(s => s.GetNetworkComplianceMetadataAsync())
                .ReturnsAsync(new NetworkComplianceMetadataResponse
                {
                    Success = true,
                    Networks = expectedNetworks
                });

            // Act
            var result = await _controller.GetNetworkComplianceMetadata();

            // Assert
            var okResult = result as OkObjectResult;
            var response = okResult!.Value as NetworkComplianceMetadataResponse;
            var aramidNetwork = response!.Networks.FirstOrDefault(n => n.Network == "aramidmain-v1.0");
            
            Assert.That(aramidNetwork, Is.Not.Null);
            Assert.That(aramidNetwork!.IsMicaReady, Is.True);
            Assert.That(aramidNetwork.RequiresWhitelisting, Is.True);
            Assert.That(aramidNetwork.RequiresRegulatoryFramework, Is.True);
        }

        [Test]
        public async Task GetNetworkComplianceMetadata_ShouldIncludeSourceMetadata()
        {
            // Arrange
            var expectedNetworks = new List<NetworkComplianceMetadata>
            {
                new NetworkComplianceMetadata
                {
                    Network = "voimain-v1.0",
                    NetworkName = "VOI Mainnet",
                    Source = "Network policy and MICA compliance guidelines",
                    ComplianceRequirements = "VOI network requires jurisdiction specification for RWA token compliance tracking."
                }
            };

            _serviceMock.Setup(s => s.GetNetworkComplianceMetadataAsync())
                .ReturnsAsync(new NetworkComplianceMetadataResponse
                {
                    Success = true,
                    Networks = expectedNetworks
                });

            // Act
            var result = await _controller.GetNetworkComplianceMetadata();

            // Assert
            var okResult = result as OkObjectResult;
            var response = okResult!.Value as NetworkComplianceMetadataResponse;
            var network = response!.Networks.First();
            
            Assert.That(network.Source, Is.Not.Null.And.Not.Empty);
            Assert.That(network.ComplianceRequirements, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task GetNetworkComplianceMetadata_ShouldSetCacheHeaders()
        {
            // Arrange
            var expectedNetworks = new List<NetworkComplianceMetadata>
            {
                new NetworkComplianceMetadata { Network = "voimain-v1.0", NetworkName = "VOI Mainnet" }
            };

            _serviceMock.Setup(s => s.GetNetworkComplianceMetadataAsync())
                .ReturnsAsync(new NetworkComplianceMetadataResponse
                {
                    Success = true,
                    Networks = expectedNetworks,
                    CacheDurationSeconds = 3600
                });

            // Act
            var result = await _controller.GetNetworkComplianceMetadata();

            // Assert
            var okResult = result as OkObjectResult;
            var response = okResult!.Value as NetworkComplianceMetadataResponse;
            
            Assert.That(response!.CacheDurationSeconds, Is.GreaterThan(0));
            Assert.That(_controller.Response.Headers.ContainsKey("Cache-Control"), Is.True);
        }

        [Test]
        public async Task GetNetworkComplianceMetadata_ServiceFailure_ShouldReturn500()
        {
            // Arrange
            _serviceMock.Setup(s => s.GetNetworkComplianceMetadataAsync())
                .ReturnsAsync(new NetworkComplianceMetadataResponse
                {
                    Success = false,
                    ErrorMessage = "Internal error"
                });

            // Act
            var result = await _controller.GetNetworkComplianceMetadata();

            // Assert
            Assert.That(result, Is.InstanceOf<ObjectResult>());
            var objectResult = result as ObjectResult;
            Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
        }

        [Test]
        public async Task GetNetworkComplianceMetadata_Exception_ShouldReturn500WithErrorMessage()
        {
            // Arrange
            _serviceMock.Setup(s => s.GetNetworkComplianceMetadataAsync())
                .ThrowsAsync(new Exception("Test exception"));

            // Act
            var result = await _controller.GetNetworkComplianceMetadata();

            // Assert
            Assert.That(result, Is.InstanceOf<ObjectResult>());
            var objectResult = result as ObjectResult;
            Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
            
            var response = objectResult.Value as NetworkComplianceMetadataResponse;
            Assert.That(response!.Success, Is.False);
            Assert.That(response.ErrorMessage, Does.Contain("Internal error"));
        }

        [Test]
        public async Task GetNetworkComplianceMetadata_ShouldIncludeAlgorandMainnet()
        {
            // Arrange
            var expectedNetworks = new List<NetworkComplianceMetadata>
            {
                new NetworkComplianceMetadata
                {
                    Network = "mainnet-v1.0",
                    NetworkName = "Algorand Mainnet",
                    IsMicaReady = true,
                    RequiresWhitelisting = false
                }
            };

            _serviceMock.Setup(s => s.GetNetworkComplianceMetadataAsync())
                .ReturnsAsync(new NetworkComplianceMetadataResponse
                {
                    Success = true,
                    Networks = expectedNetworks
                });

            // Act
            var result = await _controller.GetNetworkComplianceMetadata();

            // Assert
            var okResult = result as OkObjectResult;
            var response = okResult!.Value as NetworkComplianceMetadataResponse;
            var mainnetNetwork = response!.Networks.FirstOrDefault(n => n.Network == "mainnet-v1.0");
            
            Assert.That(mainnetNetwork, Is.Not.Null);
            Assert.That(mainnetNetwork!.IsMicaReady, Is.True);
            Assert.That(mainnetNetwork.RequiresWhitelisting, Is.False);
        }

        [Test]
        public async Task GetNetworkComplianceMetadata_TestnetShouldNotBeMicaReady()
        {
            // Arrange
            var expectedNetworks = new List<NetworkComplianceMetadata>
            {
                new NetworkComplianceMetadata
                {
                    Network = "testnet-v1.0",
                    NetworkName = "Algorand Testnet",
                    IsMicaReady = false
                }
            };

            _serviceMock.Setup(s => s.GetNetworkComplianceMetadataAsync())
                .ReturnsAsync(new NetworkComplianceMetadataResponse
                {
                    Success = true,
                    Networks = expectedNetworks
                });

            // Act
            var result = await _controller.GetNetworkComplianceMetadata();

            // Assert
            var okResult = result as OkObjectResult;
            var response = okResult!.Value as NetworkComplianceMetadataResponse;
            var testnetNetwork = response!.Networks.FirstOrDefault(n => n.Network == "testnet-v1.0");
            
            Assert.That(testnetNetwork, Is.Not.Null);
            Assert.That(testnetNetwork!.IsMicaReady, Is.False);
        }
    }
}

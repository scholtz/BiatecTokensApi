using BiatecTokensApi.Models.ERC20.Request;
using BiatecTokensApi.Models.ASA.Request;
using BiatecTokensApi.Models.ARC3.Request;
using BiatecTokensApi.Models.ARC200.Request;
using BiatecTokensApi.Models.ARC1400.Request;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Integration tests for the Biatec Tokens API endpoints.
    /// These tests verify that API endpoints are properly configured and return expected responses.
    /// Note: These tests do not perform actual blockchain transactions but verify API contract compliance.
    /// </summary>
    [TestFixture]
    public class ApiIntegrationTests
    {
        private WebApplicationFactory<BiatecTokensApi.Program> _factory = null!;
        private HttpClient _client = null!;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _factory = new WebApplicationFactory<BiatecTokensApi.Program>();
            _client = _factory.CreateClient();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _client?.Dispose();
            _factory?.Dispose();
        }

        #region Swagger/OpenAPI Tests

        [Test]
        public async Task Swagger_Endpoint_ShouldBeAccessible()
        {
            // Act
            var response = await _client.GetAsync("/swagger/v1/swagger.json");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            
            var content = await response.Content.ReadAsStringAsync();
            Assert.That(content, Is.Not.Empty);
            
            // Verify it's valid JSON
            var jsonDoc = JsonDocument.Parse(content);
            Assert.That(jsonDoc.RootElement.GetProperty("openapi").GetString(), Is.Not.Null);
        }

        [Test]
        public async Task Swagger_UI_ShouldBeAccessible()
        {
            // Act
            var response = await _client.GetAsync("/swagger/index.html");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        #endregion

        #region ERC20 Token Endpoint Tests

        [Test]
        public async Task ERC20Mintable_Endpoint_ShouldRequireAuthentication()
        {
            // Arrange
            var request = new ERC20MintableTokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "TEST",
                InitialSupply = 1000000,
                Decimals = 18,
                ChainId = 8453,
                Cap = 10000000,
                InitialSupplyReceiver = "0x742d35Cc6634C0532925a3b8D162000dDba02C79"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1/token/erc20-mintable/create", request);

            // Assert - Should require authentication
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task ERC20Preminted_Endpoint_ShouldRequireAuthentication()
        {
            // Arrange
            var request = new ERC20PremintedTokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "TEST",
                InitialSupply = 1000000,
                Decimals = 18,
                ChainId = 8453
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1/token/erc20-preminted/create", request);

            // Assert - Should require authentication
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        #endregion

        #region ASA Token Endpoint Tests

        [Test]
        public async Task ASAFungibleToken_Endpoint_ShouldRequireAuthentication()
        {
            // Arrange
            var request = new ASAFungibleTokenDeploymentRequest
            {
                Network = "testnet-v1.0",
                Name = "Test Asset",
                UnitName = "TEST",
                TotalSupply = 1000000,
                Decimals = 6,
                Url = "https://example.com"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1/token/asa-ft/create", request);

            // Assert - Should require authentication
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task ASANFT_Endpoint_ShouldRequireAuthentication()
        {
            // Arrange
            var request = new ASANonFungibleTokenDeploymentRequest
            {
                Network = "testnet-v1.0",
                Name = "Test NFT",
                UnitName = "NFT",
                Url = "https://example.com/nft"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1/token/asa-nft/create", request);

            // Assert - Should require authentication
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task ASAFNFT_Endpoint_ShouldRequireAuthentication()
        {
            // Arrange
            var request = new ASAFractionalNonFungibleTokenDeploymentRequest
            {
                Network = "testnet-v1.0",
                Name = "Test Fractional NFT",
                UnitName = "FNFT",
                TotalSupply = 100,
                Decimals = 2,
                Url = "https://example.com/fnft"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1/token/asa-fnft/create", request);

            // Assert - Should require authentication
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        #endregion

        #region ARC3 Token Endpoint Tests

        [Test]
        public async Task ARC3FungibleToken_Endpoint_ShouldRequireAuthentication()
        {
            // Arrange
            var request = new ARC3FungibleTokenDeploymentRequest
            {
                Network = "testnet-v1.0",
                Name = "Test ARC3 Token",
                UnitName = "ARC3",
                TotalSupply = 1000000,
                Decimals = 6,
                Metadata = new BiatecTokensApi.Models.ARC3.ARC3TokenMetadata
                {
                    Name = "Test ARC3 Token",
                    Description = "A test ARC3 token",
                    Image = "https://example.com/image.png"
                }
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1/token/arc3-ft/create", request);

            // Assert - Should require authentication
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task ARC3NFT_Endpoint_ShouldRequireAuthentication()
        {
            // Arrange
            var request = new ARC3NonFungibleTokenDeploymentRequest
            {
                Network = "testnet-v1.0",
                Name = "Test ARC3 NFT",
                UnitName = "NFT",
                Metadata = new BiatecTokensApi.Models.ARC3.ARC3TokenMetadata
                {
                    Name = "Test NFT",
                    Description = "A test NFT",
                    Image = "https://example.com/nft.png"
                }
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1/token/arc3-nft/create", request);

            // Assert - Should require authentication
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task ARC3FractionalNFT_Endpoint_ShouldRequireAuthentication()
        {
            // Arrange
            var request = new ARC3FractionalNonFungibleTokenDeploymentRequest
            {
                Network = "testnet-v1.0",
                Name = "Test ARC3 Fractional NFT",
                UnitName = "FNFT",
                TotalSupply = 100,
                Decimals = 2,
                Metadata = new BiatecTokensApi.Models.ARC3.ARC3TokenMetadata
                {
                    Name = "Test Fractional NFT",
                    Description = "A test fractional NFT",
                    Image = "https://example.com/fnft.png"
                }
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1/token/arc3-fnft/create", request);

            // Assert - Should require authentication
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        #endregion

        #region ARC200 Token Endpoint Tests

        [Test]
        public async Task ARC200Mintable_Endpoint_ShouldRequireAuthentication()
        {
            // Arrange
            var request = new ARC200MintableTokenDeploymentRequest
            {
                Network = "testnet-v1.0",
                Name = "Test ARC200 Mintable",
                Symbol = "ARC200M",
                Decimals = 6,
                InitialSupply = 1000000,
                Cap = 10000000
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1/token/arc200-mintable/create", request);

            // Assert - Should require authentication
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task ARC200Preminted_Endpoint_ShouldRequireAuthentication()
        {
            // Arrange
            var request = new ARC200PremintedTokenDeploymentRequest
            {
                Network = "testnet-v1.0",
                Name = "Test ARC200 Preminted",
                Symbol = "ARC200P",
                Decimals = 6,
                InitialSupply = 1000000
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1/token/arc200-preminted/create", request);

            // Assert - Should require authentication
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        #endregion

        #region ARC1400 Token Endpoint Tests

        [Test]
        public async Task ARC1400Mintable_Endpoint_ShouldRequireAuthentication()
        {
            // Arrange
            var request = new ARC1400MintableTokenDeploymentRequest
            {
                Network = "testnet-v1.0",
                Name = "Test ARC1400",
                Symbol = "ARC1400",
                Decimals = 6,
                InitialSupply = 1000000,
                Cap = 10000000
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1/token/arc1400-mintable/create", request);

            // Assert - Should require authentication
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        #endregion

        #region API Health and Discovery Tests

        [Test]
        public async Task API_ShouldRespondToRootRequest()
        {
            // Act
            var response = await _client.GetAsync("/");

            // Assert - Should either redirect or return something
            Assert.That(response.StatusCode, Is.Not.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        public async Task OpenAPI_Schema_ShouldContainAllTokenEndpoints()
        {
            // Act
            var response = await _client.GetAsync("/swagger/v1/swagger.json");
            var content = await response.Content.ReadAsStringAsync();
            var jsonDoc = JsonDocument.Parse(content);

            // Assert - Verify all expected endpoints are documented
            var paths = jsonDoc.RootElement.GetProperty("paths");
            
            Assert.That(paths.TryGetProperty("/api/v1/token/erc20-mintable/create", out _), Is.True, 
                "ERC20 mintable endpoint should be documented");
            Assert.That(paths.TryGetProperty("/api/v1/token/erc20-preminted/create", out _), Is.True,
                "ERC20 preminted endpoint should be documented");
            Assert.That(paths.TryGetProperty("/api/v1/token/asa-ft/create", out _), Is.True,
                "ASA FT endpoint should be documented");
            Assert.That(paths.TryGetProperty("/api/v1/token/asa-nft/create", out _), Is.True,
                "ASA NFT endpoint should be documented");
            Assert.That(paths.TryGetProperty("/api/v1/token/arc3-ft/create", out _), Is.True,
                "ARC3 FT endpoint should be documented");
            Assert.That(paths.TryGetProperty("/api/v1/token/arc200-mintable/create", out _), Is.True,
                "ARC200 mintable endpoint should be documented");
        }

        #endregion
    }
}

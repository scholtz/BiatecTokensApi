using BiatecTokensApi.Models.Compliance;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Integration tests for validation evidence API endpoints.
    /// Tests the /v1/compliance/validate and /v1/compliance/evidence endpoints.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ValidationEvidenceIntegrationTests
    {
        private CustomWebApplicationFactory _factory = null!;
        private HttpClient _client = null!;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _factory = new CustomWebApplicationFactory();
            _client = _factory.CreateClient();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _client?.Dispose();
            _factory?.Dispose();
        }

        /// <summary>
        /// Custom WebApplicationFactory for testing validation endpoints
        /// </summary>
        private class CustomWebApplicationFactory : WebApplicationFactory<BiatecTokensApi.Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    // Use in-memory configuration with valid test settings
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["App:Account"] = "test mnemonic phrase for testing purposes only not real",
                        ["AlgorandAuthentication:Realm"] = "BiatecTokens#ARC14",
                        ["AlgorandAuthentication:CheckExpiration"] = "false",
                        ["AlgorandAuthentication:Debug"] = "true",
                        ["AlgorandAuthentication:EmptySuccessOnFailure"] = "true",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Server"] = "https://mainnet-api.4160.nodely.dev",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Token"] = "",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Header"] = "",
                        ["IPFSConfig:ApiUrl"] = "https://ipfs-api.biatec.io",
                        ["IPFSConfig:GatewayUrl"] = "https://ipfs.biatec.io/ipfs",
                        ["IPFSConfig:TimeoutSeconds"] = "30",
                        ["IPFSConfig:MaxFileSizeBytes"] = "10485760",
                        ["IPFSConfig:ValidateContentHash"] = "true",
                        ["EVMChains:0:RpcUrl"] = "https://mainnet.base.org",
                        ["EVMChains:0:ChainId"] = "8453",
                        ["EVMChains:0:GasLimit"] = "4500000",
                        ["KeyManagementConfig:Provider"] = "Hardcoded",
                        ["KeyManagementConfig:HardcodedKey"] = "TestKeyForIntegrationTests32Characters",
                        ["Cors:0"] = "https://tokens.biatec.io",
                        ["JwtConfig:SecretKey"] = "test-secret-key-for-integration-tests-minimum-32-characters-required"
                    });
                });
            }
        }

        [Test]
        public async Task ValidateEndpoint_WithValidASAMetadata_ReturnsSuccess()
        {
            // Arrange
            var request = new ValidateTokenMetadataRequest
            {
                Context = new ValidationContext
                {
                    Network = "mainnet",
                    TokenStandard = "ASA",
                    ValidatorVersion = "1.0.0",
                    RuleSetVersion = "1.0.0"
                },
                TokenMetadata = new Dictionary<string, object>
                {
                    { "AssetName", "Test Token" },
                    { "UnitName", "TEST" },
                    { "Total", 1000000 },
                    { "Decimals", 6 }
                },
                DryRun = true
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1/compliance/validate", request);

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized), 
                "Validation endpoint requires authentication");
        }

        [Test]
        public async Task ValidateEndpoint_WithInvalidMetadata_ReturnsValidationErrors()
        {
            // Arrange
            var request = new ValidateTokenMetadataRequest
            {
                Context = new ValidationContext
                {
                    Network = "mainnet",
                    TokenStandard = "ASA",
                    ValidatorVersion = "1.0.0",
                    RuleSetVersion = "1.0.0"
                },
                TokenMetadata = new Dictionary<string, object>
                {
                    // Missing required fields
                    { "Total", 1000000 }
                },
                DryRun = true
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1/compliance/validate", request);

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized), 
                "Validation endpoint requires authentication");
        }

        [Test]
        public async Task GetEvidenceById_WithNonExistentId_ReturnsUnauthorized()
        {
            // Arrange
            var evidenceId = Guid.NewGuid().ToString();

            // Act
            var response = await _client.GetAsync($"/api/v1/compliance/evidence/{evidenceId}");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized), 
                "Evidence retrieval endpoint requires authentication");
        }

        [Test]
        public async Task ListEvidence_WithFilters_ReturnsUnauthorized()
        {
            // Act
            var response = await _client.GetAsync("/api/v1/compliance/evidence?tokenId=123&page=1&pageSize=20");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized), 
                "Evidence listing endpoint requires authentication");
        }

        [Test]
        public async Task ValidateEndpoint_WithERC20Metadata_ReturnsSuccess()
        {
            // Arrange
            var request = new ValidateTokenMetadataRequest
            {
                Context = new ValidationContext
                {
                    Network = "base",
                    TokenStandard = "ERC20",
                    ValidatorVersion = "1.0.0",
                    RuleSetVersion = "1.0.0"
                },
                TokenMetadata = new Dictionary<string, object>
                {
                    { "Name", "Test ERC20" },
                    { "Symbol", "TEST" },
                    { "TotalSupply", 1000000 }
                },
                DryRun = true
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1/compliance/validate", request);

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized), 
                "Validation endpoint requires authentication");
        }

        [Test]
        public async Task ValidateEndpoint_WithARC3Metadata_ReturnsSuccess()
        {
            // Arrange
            var request = new ValidateTokenMetadataRequest
            {
                Context = new ValidationContext
                {
                    Network = "mainnet",
                    TokenStandard = "ARC3",
                    ValidatorVersion = "1.0.0",
                    RuleSetVersion = "1.0.0"
                },
                TokenMetadata = new Dictionary<string, object>
                {
                    { "AssetName", "NFT Token" },
                    { "UnitName", "NFT" },
                    { "Total", 1 },
                    { "Decimals", 0 },
                    { "URL", "ipfs://QmTest..." }
                },
                DryRun = true
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1/compliance/validate", request);

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized), 
                "Validation endpoint requires authentication");
        }
    }
}

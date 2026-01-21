using BiatecTokensApi.Configuration;
using BiatecTokensApi.Models;
using BiatecTokensApi.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace BiatecTokensTests
{
    [TestFixture]
    [Category("RealIPFS")]
    [Description("Tests that interact with real IPFS endpoints using actual credentials")]
    public class IPFSRepositoryRealEndpointTests
    {
        private IPFSRepository? _repository;
        private ILogger<IPFSRepository>? _logger;
        private string _testSessionId;
        private readonly List<string> _uploadedHashes = new();

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // Generate a unique test session ID for this test run
            _testSessionId = $"test-{DateTime.UtcNow:yyyy-MM-dd-HH-mm-ss}-{Guid.NewGuid():N}";

            // Set up logger with more detailed output for integration tests
            using var loggerFactory = LoggerFactory.Create(builder => 
                builder.AddConsole().SetMinimumLevel(LogLevel.Information));
            _logger = loggerFactory.CreateLogger<IPFSRepository>();

            // Build configuration to get credentials from user secrets and appsettings
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true)
                .AddUserSecrets<IPFSRepositoryRealEndpointTests>()
                .Build();

            // Configure IPFS settings with real credentials
            var config = new IPFSConfig
            {
                ApiUrl = "https://ipfs-api.biatec.io",
                GatewayUrl = "https://ipfs.biatec.io/ipfs",
                Username = configuration["IPFSConfig:Username"] ?? "ipfsuser",
                Password = configuration["IPFSConfig:Password"] ?? "",
                TimeoutSeconds = 60, // Longer timeout for real API calls
                MaxFileSizeBytes = 10 * 1024 * 1024, // 10MB
                ValidateContentHash = false // Disable for integration tests to avoid IPFS multihash complexity
            };

            // Verify credentials are present
            if (string.IsNullOrEmpty(config.Username) || string.IsNullOrEmpty(config.Password))
            {
                Assert.Ignore("IPFS credentials not found. These tests require IPFSConfig:Username and IPFSConfig:Password in user secrets.");
                return;
            }

            var options = Options.Create(config);
            var httpClient = new HttpClient();
            _repository = new IPFSRepository(options, httpClient, _logger);

            Console.WriteLine($"Starting IPFS real endpoint tests with session ID: {_testSessionId}");
            Console.WriteLine($"Using IPFS API: {config.ApiUrl}");
            Console.WriteLine($"Using IPFS Gateway: {config.GatewayUrl}");
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            // Optionally clean up uploaded content (uncomment if needed)
            // CleanupUploadedContent().Wait();
            
            Console.WriteLine($"Completed IPFS real endpoint tests for session: {_testSessionId}");
            Console.WriteLine($"Total files uploaded during tests: {_uploadedHashes.Count}");
        }

        [Test, Order(1)]
        [Description("Upload plain text content to real IPFS and verify the response")]
        public async Task UploadText_ToRealIPFS_ShouldReturnValidCID()
        {
            Assert.That(_repository, Is.Not.Null);

            // Arrange
            var testContent = $"Real IPFS Test Content - Session: {_testSessionId} - Test: UploadText - Time: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss UTC}";
            var fileName = $"test-text-{_testSessionId}.txt";

            // Act
            var result = await _repository.UploadTextAsync(testContent, fileName, "text/plain");

            // Assert
            Assert.That(result.Success, Is.True, $"Upload failed: {result.ErrorMessage}");
            Assert.That(result.Hash, Is.Not.Null, "Hash should not be null");
            Assert.That(result.GatewayUrl, Is.Not.Null, "Gateway URL should not be null");
            Assert.That(result.Name, Is.EqualTo(fileName), "File name should match");
            Assert.That(result.Size > 0, Is.True, "Size should be greater than 0");
            Assert.That(result.Hash.StartsWith("Qm") || result.Hash.StartsWith("bafy"), Is.True, "Hash should be a valid IPFS CID");

            // Track uploaded hash for potential cleanup
            _uploadedHashes.Add(result.Hash);

            Console.WriteLine($"✓ Text uploaded successfully:");
            Console.WriteLine($"  CID: {result.Hash}");
            Console.WriteLine($"  Gateway URL: {result.GatewayUrl}");
            Console.WriteLine($"  Size: {result.Size} bytes");
        }

        [Test, Order(2)]
        [Description("Upload JSON object to real IPFS and verify the response")]
        public async Task UploadJsonObject_ToRealIPFS_ShouldReturnValidCID()
        {
            Assert.That(_repository, Is.Not.Null);

            // Arrange
            var testObject = new
            {
                SessionId = _testSessionId,
                TestName = "UploadJsonObject",
                Timestamp = DateTime.UtcNow,
                Data = new
                {
                    Numbers = new[] { 1, 2, 3, 42, 100 },
                    Strings = new[] { "hello", "world", "IPFS", "test" },
                    Boolean = true,
                    Nested = new { Value = "nested test value", Count = 123 }
                },
                Description = "This is a test JSON object for IPFS upload verification"
            };
            var fileName = $"test-json-{_testSessionId}.json";

            // Act
            var result = await _repository.UploadObjectAsync(testObject, fileName);

            // Assert
            Assert.That(result.Success, Is.True, $"Upload failed: {result.ErrorMessage}");
            Assert.That(result.Hash, Is.Not.Null, "Hash should not be null");
            Assert.That(result.GatewayUrl, Is.Not.Null, "Gateway URL should not be null");
            Assert.That(result.Size > 0, Is.True, "Size should be greater than 0");

            // Track uploaded hash
            _uploadedHashes.Add(result.Hash);

            Console.WriteLine($"✓ JSON object uploaded successfully:");
            Console.WriteLine($"  CID: {result.Hash}");
            Console.WriteLine($"  Gateway URL: {result.GatewayUrl}");
            Console.WriteLine($"  Size: {result.Size} bytes");
        }

        [Test, Order(3)]
        [Description("Upload content and then retrieve it to verify round-trip functionality")]
        public async Task UploadAndRetrieve_RoundTrip_ShouldPreserveContent()
        {
            Assert.That(_repository, Is.Not.Null);

            // Arrange
            var originalContent = $"Round-trip test content for session {_testSessionId}. This content should be preserved exactly during upload and retrieval. Special characters: åäö €$£ 中文 🚀";
            var fileName = $"roundtrip-test-{_testSessionId}.txt";

            // Act - Upload
            var uploadResult = await _repository.UploadTextAsync(originalContent, fileName);
            Assert.That(uploadResult.Success, Is.True, $"Upload failed: {uploadResult.ErrorMessage}");
            Assert.That(uploadResult.Hash, Is.Not.Null);

            _uploadedHashes.Add(uploadResult.Hash);

            // Wait a moment for IPFS propagation
            await Task.Delay(2000);

            // Act - Retrieve
            var retrievedContent = await _repository.RetrieveTextAsync(uploadResult.Hash);

            // Assert
            Assert.That(retrievedContent, Is.Not.Null, "Retrieved content should not be null");
            Assert.That(retrievedContent, Is.EqualTo(originalContent), "Retrieved content should exactly match original content");

            Console.WriteLine($"✓ Round-trip test successful:");
            Console.WriteLine($"  CID: {uploadResult.Hash}");
            Console.WriteLine($"  Original length: {originalContent.Length} characters");
            Console.WriteLine($"  Retrieved length: {retrievedContent.Length} characters");
            Console.WriteLine($"  Content matches: {originalContent == retrievedContent}");
        }

        [Test, Order(4)]
        [Description("Upload an ARC3 metadata object and retrieve it as a typed object")]
        public async Task UploadAndRetrieveARC3Metadata_ShouldPreserveStructure()
        {
            Assert.That(_repository, Is.Not.Null);

            // Arrange - Create a sample ARC3 metadata object
            var metadata = new ARC3TokenMetadata
            {
                Name = $"Test Token - {_testSessionId}",
                Description = "This is a test ARC3 token metadata for IPFS storage verification",
                Image = "https://example.com/token-image.png",
                ImageMimetype = "image/png",
                BackgroundColor = "FF5733",
                ExternalUrl = "https://example.com/token-info",
                ExternalUrlMimetype = "text/html",
                Properties = new Dictionary<string, object>
                {
                    ["sessionId"] = _testSessionId,
                    ["testType"] = "ARC3Metadata",
                    ["number"] = 42,
                    ["array"] = new[] { "item1", "item2", "item3" }
                }
            };

            // Act - Upload
            var uploadResult = await _repository.UploadObjectAsync(metadata, $"arc3-metadata-{_testSessionId}.json");
            Assert.That(uploadResult.Success, Is.True, $"Upload failed: {uploadResult.ErrorMessage}");
            Assert.That(uploadResult.Hash, Is.Not.Null);

            _uploadedHashes.Add(uploadResult.Hash);

            // Wait for propagation
            await Task.Delay(2000);

            // Act - Retrieve as typed object
            var retrievedMetadata = await _repository.RetrieveObjectAsync<ARC3TokenMetadata>(uploadResult.Hash);

            // Assert
            Assert.That(retrievedMetadata, Is.Not.Null, "Retrieved metadata should not be null");
            Assert.That(retrievedMetadata.Name, Is.EqualTo(metadata.Name), "Name should match");
            Assert.That(retrievedMetadata.Description, Is.EqualTo(metadata.Description), "Description should match");
            Assert.That(retrievedMetadata.Image, Is.EqualTo(metadata.Image), "Image URL should match");
            Assert.That(retrievedMetadata.BackgroundColor, Is.EqualTo(metadata.BackgroundColor), "Background color should match");
            Assert.That(retrievedMetadata.Properties, Is.Not.Null, "Properties should not be null");

            Console.WriteLine($"✓ ARC3 metadata round-trip successful:");
            Console.WriteLine($"  CID: {uploadResult.Hash}");
            Console.WriteLine($"  Token Name: {retrievedMetadata.Name}");
            Console.WriteLine($"  Properties count: {retrievedMetadata.Properties?.Count ?? 0}");
        }

        [Test, Order(5)]
        [Description("Test content existence check functionality")]
        public async Task CheckContentExists_WithValidCID_ShouldReturnTrue()
        {
            Assert.That(_repository, Is.Not.Null);

            // Arrange - Upload content first
            var testContent = $"Content existence test - {_testSessionId}";
            var uploadResult = await _repository.UploadTextAsync(testContent, $"exists-test-{_testSessionId}.txt");
            Assert.That(uploadResult.Success, Is.True);
            Assert.That(uploadResult.Hash, Is.Not.Null);

            _uploadedHashes.Add(uploadResult.Hash);

            // Wait for propagation
            await Task.Delay(2000);

            // Act
            var exists = await _repository.ExistsAsync(uploadResult.Hash);

            // Assert
            Assert.That(exists, Is.True, "Content should exist in IPFS");

            Console.WriteLine($"✓ Content existence check successful:");
            Console.WriteLine($"  CID: {uploadResult.Hash}");
            Console.WriteLine($"  Exists: {exists}");
        }

        [Test, Order(6)]
        [Description("Test content info retrieval functionality")]
        public async Task GetContentInfo_WithValidCID_ShouldReturnCorrectInfo()
        {
            Assert.That(_repository, Is.Not.Null);

            // Arrange - Upload content first
            var testContent = $"Content info test - {_testSessionId} - " + new string('X', 1000); // Make it larger for better size testing
            var uploadResult = await _repository.UploadTextAsync(testContent, $"info-test-{_testSessionId}.txt", "text/plain");
            Assert.That(uploadResult.Success, Is.True);
            Assert.That(uploadResult.Hash, Is.Not.Null);

            _uploadedHashes.Add(uploadResult.Hash);

            // Wait for propagation
            await Task.Delay(2000);

            // Act
            var contentInfo = await _repository.GetContentInfoAsync(uploadResult.Hash);

            // Assert
            Assert.That(contentInfo, Is.Not.Null, "Content info should not be null");
            Assert.That(contentInfo.Hash, Is.EqualTo(uploadResult.Hash), "Hash should match");
            Assert.That(contentInfo.Size > 0, Is.True, "Size should be greater than 0");
            Assert.That(contentInfo.GatewayUrl, Is.Not.Null, "Gateway URL should not be null");
            Assert.That(contentInfo.GatewayUrl.Contains(uploadResult.Hash), Is.True, "Gateway URL should contain the hash");

            Console.WriteLine($"✓ Content info retrieval successful:");
            Console.WriteLine($"  CID: {contentInfo.Hash}");
            Console.WriteLine($"  Size: {contentInfo.Size} bytes");
            Console.WriteLine($"  Content Type: {contentInfo.ContentType ?? "not specified"}");
            Console.WriteLine($"  Gateway URL: {contentInfo.GatewayUrl}");
        }

        [Test, Order(7)]
        [Description("Test pinning functionality with real IPFS")]
        public async Task PinContent_WithValidCID_ShouldSucceed()
        {
            Assert.That(_repository, Is.Not.Null);

            // Arrange - Upload content first
            var testContent = $"Pin test content - {_testSessionId}";
            var uploadResult = await _repository.UploadTextAsync(testContent, $"pin-test-{_testSessionId}.txt");
            Assert.That(uploadResult.Success, Is.True);
            Assert.That(uploadResult.Hash, Is.Not.Null);

            _uploadedHashes.Add(uploadResult.Hash);

            // Wait for propagation
            await Task.Delay(2000);

            // Act
            var pinResult = await _repository.PinAsync(uploadResult.Hash);

            // Assert
            Assert.That(pinResult, Is.True, "Pinning should succeed");

            Console.WriteLine($"✓ Content pinning successful:");
            Console.WriteLine($"  CID: {uploadResult.Hash}");
            Console.WriteLine($"  Pinned: {pinResult}");
        }

        [Test, Order(8)]
        [Description("Test error handling with invalid CID")]
        public async Task RetrieveContent_WithInvalidCID_ShouldHandleGracefully()
        {
            Assert.That(_repository, Is.Not.Null);

            // Arrange
            var invalidCid = "QmInvalidCidThatDoesNotExist123456789abcdef";

            // Act
            var result = await _repository.RetrieveAsync(invalidCid);
            var textResult = await _repository.RetrieveTextAsync(invalidCid);
            var objectResult = await _repository.RetrieveObjectAsync<ARC3TokenMetadata>(invalidCid);
            var existsResult = await _repository.ExistsAsync(invalidCid);
            var infoResult = await _repository.GetContentInfoAsync(invalidCid);

            // Assert
            Assert.That(result.Success, Is.False, "Retrieve should fail for invalid CID");
            Assert.That(textResult, Is.Null, "Text retrieve should return null for invalid CID");
            Assert.That(objectResult, Is.Null, "Object retrieve should return null for invalid CID");
            Assert.That(existsResult, Is.False, "Exists should return false for invalid CID");
            Assert.That(infoResult, Is.Null, "Info should return null for invalid CID");

            Console.WriteLine($"✓ Invalid CID handling verified:");
            Console.WriteLine($"  Invalid CID: {invalidCid}");
            Console.WriteLine($"  All operations properly returned null/false");
        }

        [Test, Order(9)]
        [Description("Test large content upload (within limits)")]
        public async Task UploadLargeContent_WithinLimits_ShouldSucceed()
        {
            Assert.That(_repository, Is.Not.Null);

            // Arrange - Create content close to but under the 10MB limit
            var largeContentSize = 5 * 1024 * 1024; // 5MB
            var largeContent = new byte[largeContentSize];
            var random = new Random(42); // Use seed for reproducible content
            random.NextBytes(largeContent);

            // Add some identifying header
            var header = Encoding.UTF8.GetBytes($"Large content test - {_testSessionId} - Size: {largeContentSize} bytes\n");
            Array.Copy(header, largeContent, header.Length);

            var request = new IPFSUploadRequest
            {
                Content = largeContent,
                FileName = $"large-content-{_testSessionId}.bin",
                ContentType = "application/octet-stream",
                Pin = true
            };

            // Act
            var result = await _repository.UploadAsync(request);

            // Assert
            Assert.That(result.Success, Is.True, $"Large content upload failed: {result.ErrorMessage}");
            Assert.That(result.Hash, Is.Not.Null);
            
            // IPFS adds metadata/overhead, so the reported size will be slightly larger than original content
            // Verify the size is reasonable (within 1% of original + some reasonable overhead)
            var expectedMinSize = largeContentSize;
            var expectedMaxSize = largeContentSize + (largeContentSize / 100) + 10000; // Allow 1% + 10KB overhead
            Assert.That(result.Size >= expectedMinSize, Is.True, 
                $"IPFS size ({result.Size}) should be at least the original content size ({largeContentSize})");
            Assert.That(result.Size <= expectedMaxSize, Is.True, 
                $"IPFS size ({result.Size}) should not exceed reasonable overhead limit ({expectedMaxSize})");

            _uploadedHashes.Add(result.Hash);

            Console.WriteLine($"✓ Large content upload successful:");
            Console.WriteLine($"  CID: {result.Hash}");
            Console.WriteLine($"  Original content size: {largeContentSize:N0} bytes ({largeContentSize / 1024.0 / 1024.0:F2} MB)");
            Console.WriteLine($"  IPFS reported size: {result.Size:N0} bytes ({result.Size / 1024.0 / 1024.0:F2} MB)");
            Console.WriteLine($"  Overhead: {result.Size - largeContentSize:N0} bytes ({(result.Size - largeContentSize) / (double)largeContentSize * 100:F2}%)");
        }

        [Test, Order(10)]
        [Description("Verify gateway URL accessibility")]
        public async Task VerifyGatewayURLs_ShouldBeAccessible()
        {
            Assert.That(_repository, Is.Not.Null);

            if (_uploadedHashes.Count == 0)
            {
                Assert.Ignore("No content has been uploaded yet to test gateway URLs");
                return;
            }

            // Test the first uploaded hash
            var testHash = _uploadedHashes[0];
            var gatewayUrl = $"https://ipfs.biatec.io/ipfs/{testHash}";

            // Act - Try to access the content via gateway
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);

            var response = await httpClient.GetAsync(gatewayUrl);

            // Assert
            Assert.That(response.IsSuccessStatusCode, Is.True, $"Gateway URL should be accessible: {gatewayUrl}");
            Assert.That(response.Content.Headers.ContentLength > 0, Is.True, "Content should have size > 0");

            var content = await response.Content.ReadAsStringAsync();
            Assert.That(content, Is.Not.Null, "Content should not be null");
            Assert.That(content.Length > 0, Is.True, "Content should not be empty");

            Console.WriteLine($"✓ Gateway URL accessibility verified:");
            Console.WriteLine($"  URL: {gatewayUrl}");
            Console.WriteLine($"  Status: {response.StatusCode}");
            Console.WriteLine($"  Content Length: {response.Content.Headers.ContentLength} bytes");
            Console.WriteLine($"  Content Type: {response.Content.Headers.ContentType}");
        }

        // Helper method for potential cleanup (currently unused but available)
        private async Task CleanupUploadedContent()
        {
            if (_repository == null || _uploadedHashes.Count == 0)
                return;

            Console.WriteLine($"Cleaning up {_uploadedHashes.Count} uploaded files...");

            foreach (var hash in _uploadedHashes)
            {
                try
                {
                    await _repository.UnpinAsync(hash);
                    Console.WriteLine($"Unpinned: {hash}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to unpin {hash}: {ex.Message}");
                }
            }
        }

        // Helper class for testing (reuse the ARC3 metadata from the main project)
        private class ARC3TokenMetadata
        {
            public string? Name { get; set; }
            public string? Description { get; set; }
            public string? Image { get; set; }
            public string? ImageMimetype { get; set; }
            public string? BackgroundColor { get; set; }
            public string? ExternalUrl { get; set; }
            public string? ExternalUrlMimetype { get; set; }
            public Dictionary<string, object>? Properties { get; set; }
        }
    }
}
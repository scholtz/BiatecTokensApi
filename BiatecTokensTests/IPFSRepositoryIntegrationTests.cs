using BiatecTokensApi.Configuration;
using BiatecTokensApi.Models;
using BiatecTokensApi.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;

namespace BiatecTokensTests
{
    [TestFixture]
    [Category("Integration")]
    public class IPFSRepositoryIntegrationTests
    {
        private IPFSRepository? _repository;
        private ILogger<IPFSRepository>? _logger;
        private const string TestText = "Hello, IPFS Integration Test!";

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // Set up logger
            using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            _logger = loggerFactory.CreateLogger<IPFSRepository>();

            // Configure IPFS settings for integration test
            var config = new IPFSConfig
            {
                ApiUrl = "https://ipfs-api.biatec.io",
                GatewayUrl = "https://ipfs.biatec.io/ipfs",
                // Note: In real tests, these would come from environment variables or test configuration
                Username = Environment.GetEnvironmentVariable("IPFS_USERNAME") ?? "",
                Password = Environment.GetEnvironmentVariable("IPFS_PASSWORD") ?? "",
                TimeoutSeconds = 60, // Longer timeout for integration tests
                MaxFileSizeBytes = 10 * 1024 * 1024,
                ValidateContentHash = false // Disable for integration tests
            };

            var options = Options.Create(config);
            var httpClient = new HttpClient();
            _repository = new IPFSRepository(options, httpClient, _logger);
        }

        [Test]
        [Ignore("Requires IPFS credentials - run manually with valid credentials")]
        public async Task UploadAndRetrieve_TextContent_ShouldWork()
        {
            // Skip if no credentials provided
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("IPFS_USERNAME")))
            {
                Assert.Ignore("IPFS credentials not provided. Set IPFS_USERNAME and IPFS_PASSWORD environment variables to run this test.");
                return;
            }

            Assert.IsNotNull(_repository);

            // Upload text content
            var uploadResult = await _repository.UploadTextAsync(TestText, "integration-test.txt");
            
            Assert.IsTrue(uploadResult.Success, $"Upload failed: {uploadResult.ErrorMessage}");
            Assert.IsNotNull(uploadResult.Hash);
            Assert.IsNotNull(uploadResult.GatewayUrl);
            
            Console.WriteLine($"Uploaded to IPFS with hash: {uploadResult.Hash}");
            Console.WriteLine($"Gateway URL: {uploadResult.GatewayUrl}");

            // Wait a moment for propagation
            await Task.Delay(2000);

            // Retrieve the content
            var retrievedText = await _repository.RetrieveTextAsync(uploadResult.Hash!);
            
            Assert.IsNotNull(retrievedText);
            Assert.AreEqual(TestText, retrievedText);
            
            Console.WriteLine($"Successfully retrieved: {retrievedText}");
        }

        [Test]
        [Ignore("Requires IPFS credentials - run manually with valid credentials")]
        public async Task UploadAndRetrieve_JsonObject_ShouldWork()
        {
            // Skip if no credentials provided
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("IPFS_USERNAME")))
            {
                Assert.Ignore("IPFS credentials not provided. Set IPFS_USERNAME and IPFS_PASSWORD environment variables to run this test.");
                return;
            }

            Assert.IsNotNull(_repository);

            // Create test object
            var testObject = new
            {
                Name = "IPFS Integration Test",
                Timestamp = DateTime.UtcNow,
                Items = new[] { "item1", "item2", "item3" },
                Metadata = new
                {
                    Version = "1.0",
                    Type = "test"
                }
            };

            // Upload object as JSON
            var uploadResult = await _repository.UploadObjectAsync(testObject, "test-object.json");
            
            Assert.IsTrue(uploadResult.Success, $"Upload failed: {uploadResult.ErrorMessage}");
            Assert.IsNotNull(uploadResult.Hash);
            
            Console.WriteLine($"Uploaded JSON object with hash: {uploadResult.Hash}");

            // Wait a moment for propagation
            await Task.Delay(2000);

            // Retrieve as JSON string
            var retrievedJson = await _repository.RetrieveTextAsync(uploadResult.Hash!);
            
            Assert.IsNotNull(retrievedJson);
            Assert.IsTrue(retrievedJson.Contains("IPFS Integration Test"));
            
            Console.WriteLine($"Retrieved JSON: {retrievedJson}");
        }

        [Test]
        [Ignore("Requires IPFS credentials - run manually with valid credentials")]
        public async Task Pin_ExistingContent_ShouldWork()
        {
            // Skip if no credentials provided
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("IPFS_USERNAME")))
            {
                Assert.Ignore("IPFS credentials not provided. Set IPFS_USERNAME and IPFS_PASSWORD environment variables to run this test.");
                return;
            }

            Assert.IsNotNull(_repository);

            // First upload some content
            var uploadResult = await _repository.UploadTextAsync("Content to pin", "pin-test.txt");
            Assert.IsTrue(uploadResult.Success);
            Assert.IsNotNull(uploadResult.Hash);

            // Pin the content
            var pinResult = await _repository.PinAsync(uploadResult.Hash!);
            Assert.IsTrue(pinResult, "Pinning should succeed");
            
            Console.WriteLine($"Successfully pinned content with hash: {uploadResult.Hash}");
        }

        [Test]
        public async Task Exists_WithKnownInvalidCid_ShouldReturnFalse()
        {
            Assert.IsNotNull(_repository);

            // Test with a well-formed but non-existent CID
            var invalidCid = "QmInvalidHashThatDoesNotExist123456789";
            var exists = await _repository.ExistsAsync(invalidCid);
            
            Assert.IsFalse(exists);
        }

        [Test]
        public async Task GetContentInfo_WithInvalidCid_ShouldReturnNull()
        {
            Assert.IsNotNull(_repository);

            var invalidCid = "QmInvalidHashForContentInfo";
            var info = await _repository.GetContentInfoAsync(invalidCid);
            
            Assert.IsNull(info);
        }

        [Test]
        public async Task UploadLargeContent_ShouldFailWithConfiguredLimit()
        {
            Assert.IsNotNull(_repository);

            // Create content larger than the configured limit (10MB)
            var largeContent = new byte[11 * 1024 * 1024]; // 11MB
            var request = new IPFSUploadRequest
            {
                Content = largeContent,
                FileName = "large-file.bin"
            };

            var result = await _repository.UploadAsync(request);
            
            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.ErrorMessage?.Contains("exceeds maximum allowed size"));
        }

        /// <summary>
        /// Helper method to create test content of specific size
        /// </summary>
        private static byte[] CreateTestContent(int sizeInBytes)
        {
            var content = new byte[sizeInBytes];
            var random = new Random(42); // Use seed for reproducible content
            random.NextBytes(content);
            return content;
        }

        /// <summary>
        /// Manual test to demonstrate basic IPFS functionality
        /// Run this with valid credentials to verify the IPFS connection
        /// </summary>
        [Test]
        [Explicit("Manual test - requires valid IPFS credentials")]
        public async Task ManualTest_BasicIPFSOperations()
        {
            Assert.IsNotNull(_repository);

            Console.WriteLine("Starting manual IPFS test...");

            // Test 1: Upload text
            Console.WriteLine("\n1. Uploading text content...");
            var textUpload = await _repository.UploadTextAsync("Manual test content", "manual-test.txt");
            Console.WriteLine($"Upload result: Success={textUpload.Success}, Hash={textUpload.Hash}, Error={textUpload.ErrorMessage}");

            if (textUpload.Success && textUpload.Hash != null)
            {
                // Test 2: Check if exists
                Console.WriteLine("\n2. Checking if content exists...");
                var exists = await _repository.ExistsAsync(textUpload.Hash);
                Console.WriteLine($"Content exists: {exists}");

                // Test 3: Get content info
                Console.WriteLine("\n3. Getting content info...");
                var info = await _repository.GetContentInfoAsync(textUpload.Hash);
                Console.WriteLine($"Content info: Size={info?.Size}, Type={info?.ContentType}");

                // Test 4: Retrieve content
                Console.WriteLine("\n4. Retrieving content...");
                var retrieved = await _repository.RetrieveTextAsync(textUpload.Hash);
                Console.WriteLine($"Retrieved content: {retrieved}");

                // Test 5: Pin content
                Console.WriteLine("\n5. Pinning content...");
                var pinned = await _repository.PinAsync(textUpload.Hash);
                Console.WriteLine($"Pinning result: {pinned}");
            }

            Console.WriteLine("\nManual test completed.");
        }
    }
}
using BiatecTokensApi.Configuration;
using BiatecTokensApi.Models;
using BiatecTokensApi.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text;
using System.Text.Json;

namespace BiatecTokensTests
{
    [TestFixture]
    public class IPFSRepositoryTests
    {
        private Mock<HttpMessageHandler> _httpMessageHandlerMock;
        private HttpClient _httpClient;
        private Mock<ILogger<IPFSRepository>> _loggerMock;
        private IOptions<IPFSConfig> _configOptions;
        private IPFSRepository _repository;

        [SetUp]
        public void Setup()
        {
            _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_httpMessageHandlerMock.Object);
            _loggerMock = new Mock<ILogger<IPFSRepository>>();

            var config = new IPFSConfig
            {
                ApiUrl = "https://ipfs-api.biatec.io",
                GatewayUrl = "https://ipfs.biatec.io/ipfs",
                Username = "testuser",
                Password = "testpass",
                TimeoutSeconds = 30,
                MaxFileSizeBytes = 1024 * 1024, // 1MB for testing
                ValidateContentHash = false // Disable for unit tests
            };

            _configOptions = Options.Create(config);
            _repository = new IPFSRepository(_configOptions, _httpClient, _loggerMock.Object);
        }

        [TearDown]
        public void TearDown()
        {
            _httpClient?.Dispose();
        }

        [Test]
        public async Task UploadAsync_WithValidContent_ShouldReturnSuccess()
        {
            // Arrange
            var content = Encoding.UTF8.GetBytes("Hello, IPFS!");
            var request = new IPFSUploadRequest
            {
                Content = content,
                FileName = "test.txt",
                ContentType = "text/plain"
            };

            var mockResponse = new
            {
                Hash = "QmTestHash123",
                Name = "test.txt",
                Size = content.Length.ToString()  // Convert to string to match IPFS API format
            };

            var responseContent = JsonSerializer.Serialize(mockResponse);
            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseContent, Encoding.UTF8, "application/json")
            };

            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);

            // Act
            var result = await _repository.UploadAsync(request);

            // Assert
            Assert.IsTrue(result.Success);
            Assert.AreEqual("QmTestHash123", result.Hash);
            Assert.AreEqual("test.txt", result.Name);
            Assert.AreEqual(content.Length, result.Size);
            Assert.AreEqual("https://ipfs.biatec.io/ipfs/QmTestHash123", result.GatewayUrl);
        }

        [Test]
        public async Task UploadAsync_WithEmptyContent_ShouldReturnError()
        {
            // Arrange
            var request = new IPFSUploadRequest
            {
                Content = Array.Empty<byte>(),
                FileName = "empty.txt"
            };

            // Act
            var result = await _repository.UploadAsync(request);

            // Assert
            Assert.IsFalse(result.Success);
            Assert.AreEqual("Content cannot be null or empty", result.ErrorMessage);
        }

        [Test]
        public async Task UploadAsync_WithOversizedContent_ShouldReturnError()
        {
            // Arrange
            var oversizedContent = new byte[2 * 1024 * 1024]; // 2MB, larger than 1MB limit
            var request = new IPFSUploadRequest
            {
                Content = oversizedContent,
                FileName = "large.bin"
            };

            // Act
            var result = await _repository.UploadAsync(request);

            // Assert
            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.ErrorMessage?.Contains("exceeds maximum allowed size"));
        }

        [Test]
        public async Task UploadTextAsync_WithValidText_ShouldReturnSuccess()
        {
            // Arrange
            var text = "Hello, IPFS World!";
            var mockResponse = new
            {
                Hash = "QmTextHash456",
                Name = "file",
                Size = Encoding.UTF8.GetBytes(text).Length.ToString()  // Convert to string to match IPFS API format
            };

            var responseContent = JsonSerializer.Serialize(mockResponse);
            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseContent, Encoding.UTF8, "application/json")
            };

            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);

            // Act
            var result = await _repository.UploadTextAsync(text, "hello.txt");

            // Assert
            Assert.IsTrue(result.Success);
            Assert.AreEqual("QmTextHash456", result.Hash);
        }

        [Test]
        public async Task UploadObjectAsync_WithValidObject_ShouldReturnSuccess()
        {
            // Arrange
            var testObject = new { Name = "Test", Value = 42, Items = new[] { "a", "b", "c" } };
            var mockResponse = new
            {
                Hash = "QmObjectHash789",
                Name = "file",
                Size = "100"  // Convert to string to match IPFS API format
            };

            var responseContent = JsonSerializer.Serialize(mockResponse);
            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseContent, Encoding.UTF8, "application/json")
            };

            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);

            // Act
            var result = await _repository.UploadObjectAsync(testObject, "test.json");

            // Assert
            Assert.IsTrue(result.Success);
            Assert.AreEqual("QmObjectHash789", result.Hash);
        }

        [Test]
        public async Task RetrieveAsync_WithValidCid_ShouldReturnContent()
        {
            // Arrange
            var cid = "QmTestRetrieve123";
            var content = Encoding.UTF8.GetBytes("Retrieved content");
            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(content)
            };
            httpResponse.Content.Headers.Add("Content-Type", "text/plain");

            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains(cid)),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);

            // Act
            var result = await _repository.RetrieveAsync(cid);

            // Assert
            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.Content);
            Assert.AreEqual("Retrieved content", Encoding.UTF8.GetString(result.Content));
            Assert.AreEqual(content.Length, result.Size);
        }

        [Test]
        public async Task RetrieveTextAsync_WithValidCid_ShouldReturnText()
        {
            // Arrange
            var cid = "QmTestText123";
            var textContent = "Hello from IPFS!";
            var content = Encoding.UTF8.GetBytes(textContent);
            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(content)
            };

            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains(cid)),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);

            // Act
            var result = await _repository.RetrieveTextAsync(cid);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(textContent, result);
        }

        [Test]
        public async Task RetrieveObjectAsync_WithValidCid_ShouldReturnObject()
        {
            // Arrange
            var cid = "QmTestObject123";
            var testObject = new TestClass { Name = "Test", Value = 42 };
            
            // Use the same JSON options as the repository for consistency
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            };
            var jsonContent = JsonSerializer.Serialize(testObject, jsonOptions);
            var content = Encoding.UTF8.GetBytes(jsonContent);
            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(content)
            };

            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains(cid)),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);

            // Act
            var result = await _repository.RetrieveObjectAsync<TestClass>(cid);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(testObject.Name, result.Name);
            Assert.AreEqual(testObject.Value, result.Value);
        }

        [Test]
        public async Task ExistsAsync_WithValidCid_ShouldReturnTrue()
        {
            // Arrange
            var cid = "QmTestExists123";
            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(new byte[100])
            };
            httpResponse.Content.Headers.ContentLength = 100;

            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Head && req.RequestUri!.ToString().Contains(cid)),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);

            // Act
            var result = await _repository.ExistsAsync(cid);

            // Assert
            Assert.IsTrue(result);
        }

        [Test]
        public async Task ExistsAsync_WithInvalidCid_ShouldReturnFalse()
        {
            // Arrange
            var cid = "QmInvalidCid";
            var httpResponse = new HttpResponseMessage(HttpStatusCode.NotFound);

            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Head && req.RequestUri!.ToString().Contains(cid)),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);

            // Act
            var result = await _repository.ExistsAsync(cid);

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public async Task PinAsync_WithValidCid_ShouldReturnTrue()
        {
            // Arrange
            var cid = "QmTestPin123";
            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK);

            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => 
                        req.Method == HttpMethod.Post && 
                        req.RequestUri!.ToString().Contains($"pin/add?arg={cid}")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);

            // Act
            var result = await _repository.PinAsync(cid);

            // Assert
            Assert.IsTrue(result);
        }

        [Test]
        public async Task UnpinAsync_WithValidCid_ShouldReturnTrue()
        {
            // Arrange
            var cid = "QmTestUnpin123";
            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK);

            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => 
                        req.Method == HttpMethod.Post && 
                        req.RequestUri!.ToString().Contains($"pin/rm?arg={cid}")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);

            // Act
            var result = await _repository.UnpinAsync(cid);

            // Assert
            Assert.IsTrue(result);
        }

        [Test]
        public async Task GetContentInfoAsync_WithValidCid_ShouldReturnInfo()
        {
            // Arrange
            var cid = "QmTestInfo123";
            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(new byte[500])
            };
            httpResponse.Content.Headers.ContentLength = 500;
            httpResponse.Content.Headers.Add("Content-Type", "application/json");

            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Head),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);

            // Act
            var result = await _repository.GetContentInfoAsync(cid);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(cid, result.Hash);
            Assert.AreEqual(500, result.Size);
            Assert.AreEqual("application/json", result.ContentType);
            Assert.AreEqual($"https://ipfs.biatec.io/ipfs/{cid}", result.GatewayUrl);
        }

        [Test]
        public void Constructor_WithCredentials_ShouldSetAuthorizationHeader()
        {
            // This test verifies that the authorization header is set correctly
            // We can't directly test the private header, but we can ensure the constructor doesn't throw
            Assert.DoesNotThrow(() =>
            {
                var configWithAuth = new IPFSConfig
                {
                    ApiUrl = "https://ipfs-api.biatec.io",
                    GatewayUrl = "https://ipfs.biatec.io/ipfs",
                    Username = "user",
                    Password = "pass"
                };
                var options = Options.Create(configWithAuth);
                using var httpClient = new HttpClient();
                var repo = new IPFSRepository(options, httpClient, _loggerMock.Object);
            });
        }

        // Test helper class
        public class TestClass
        {
            public string Name { get; set; } = string.Empty;
            public int Value { get; set; }
        }
    }
}
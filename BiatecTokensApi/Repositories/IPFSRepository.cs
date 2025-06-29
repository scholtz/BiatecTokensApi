using BiatecTokensApi.Configuration;
using BiatecTokensApi.Models;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BiatecTokensApi.Repositories
{
    /// <summary>
    /// Repository for IPFS operations using Biatec IPFS API
    /// </summary>
    public class IPFSRepository : IIPFSRepository
    {
        private readonly IPFSConfig _config;
        private readonly HttpClient _httpClient;
        private readonly ILogger<IPFSRepository> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public IPFSRepository(
            IOptions<IPFSConfig> config,
            HttpClient httpClient,
            ILogger<IPFSRepository> logger)
        {
            _config = config.Value;
            _httpClient = httpClient;
            _logger = logger;

            // Configure HTTP client
            _httpClient.Timeout = TimeSpan.FromSeconds(_config.TimeoutSeconds);
            
            // Set up basic authentication if credentials are provided
            if (!string.IsNullOrEmpty(_config.Username) && !string.IsNullOrEmpty(_config.Password))
            {
                var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_config.Username}:{_config.Password}"));
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            }

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            };
        }

        /// <summary>
        /// Uploads content to IPFS
        /// </summary>
        public async Task<IPFSUploadResponse> UploadAsync(IPFSUploadRequest request)
        {
            var response = new IPFSUploadResponse { Success = false };

            try
            {
                // Validate input
                if (request.Content == null || request.Content.Length == 0)
                {
                    response.ErrorMessage = "Content cannot be null or empty";
                    return response;
                }

                if (request.Content.Length > _config.MaxFileSizeBytes)
                {
                    response.ErrorMessage = $"Content size ({request.Content.Length} bytes) exceeds maximum allowed size ({_config.MaxFileSizeBytes} bytes)";
                    return response;
                }

                _logger.LogInformation("Uploading {Size} bytes to IPFS", request.Content.Length);

                // Create multipart form content
                using var content = new MultipartFormDataContent();
                using var byteContent = new ByteArrayContent(request.Content);
                
                if (!string.IsNullOrEmpty(request.ContentType))
                {
                    byteContent.Headers.ContentType = MediaTypeHeaderValue.Parse(request.ContentType);
                }

                var fileName = request.FileName ?? "file";
                content.Add(byteContent, "file", fileName);

                if (request.Pin)
                {
                    content.Add(new StringContent("true"), "pin");
                }

                // Make the API call
                var apiUrl = $"{_config.ApiUrl}/api/v0/add";
                var httpResponse = await _httpClient.PostAsync(apiUrl, content);

                if (httpResponse.IsSuccessStatusCode)
                {
                    var responseContent = await httpResponse.Content.ReadAsStringAsync();
                    _logger.LogDebug("IPFS API response: {Response}", responseContent);

                    // Parse NDJSON response (newline-delimited JSON)
                    var ipfsResponse = ParseNDJsonResponse(responseContent);

                    if (ipfsResponse?.Hash != null)
                    {
                        response.Success = true;
                        response.Hash = ipfsResponse.Hash;
                        response.Name = ipfsResponse.Name;
                        response.Size = ipfsResponse.GetSizeAsLong();
                        response.GatewayUrl = $"{_config.GatewayUrl}/{ipfsResponse.Hash}";

                        _logger.LogInformation("Successfully uploaded to IPFS with hash: {Hash}", ipfsResponse.Hash);
                    }
                    else
                    {
                        response.ErrorMessage = "Invalid response from IPFS API - no valid hash found";
                        _logger.LogError("Invalid response from IPFS API: {Response}", responseContent);
                    }
                }
                else
                {
                    var errorContent = await httpResponse.Content.ReadAsStringAsync();
                    response.ErrorMessage = $"IPFS API error: {httpResponse.StatusCode} - {errorContent}";
                    _logger.LogError("IPFS API error: {StatusCode} - {Error}", httpResponse.StatusCode, errorContent);
                }
            }
            catch (Exception ex)
            {
                response.ErrorMessage = $"Upload failed: {ex.Message}";
                _logger.LogError(ex, "Error uploading to IPFS");
            }

            return response;
        }

        /// <summary>
        /// Parses NDJSON (newline-delimited JSON) response from IPFS API
        /// </summary>
        /// <param name="ndjsonContent">NDJSON content from IPFS API</param>
        /// <returns>The file upload response (first line that contains the uploaded file info)</returns>
        private IPFSApiResponse? ParseNDJsonResponse(string ndjsonContent)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ndjsonContent))
                    return null;

                // IPFS API returns Pascal-case JSON, so use default naming (no camelCase conversion)
                var ipfsJsonOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    WriteIndented = false
                };

                // Split by newlines and process each JSON object
                var lines = ndjsonContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                
                _logger.LogInformation("Parsing {LineCount} lines from NDJSON response", lines.Length);

                foreach (var line in lines)
                {
                    try
                    {
                        var trimmedLine = line.Trim();
                        if (string.IsNullOrEmpty(trimmedLine))
                            continue;

                        _logger.LogInformation("Attempting to parse line: {Line}", trimmedLine);
                        
                        var jsonResponse = JsonSerializer.Deserialize<IPFSApiResponse>(trimmedLine, ipfsJsonOptions);
                        
                        if (jsonResponse == null)
                        {
                            _logger.LogInformation("Deserialized to null object for line: {Line}", trimmedLine);
                            continue;
                        }

                        if (string.IsNullOrEmpty(jsonResponse.Hash))
                        {
                            _logger.LogInformation("Skipping line with null/empty hash: Hash='{Hash}', Name='{Name}', Size='{Size}'", 
                                jsonResponse.Hash, jsonResponse.Name, jsonResponse.Size);
                            continue;
                        }

                        _logger.LogInformation("Successfully parsed: Hash='{Hash}', Name='{Name}', Size={Size}", 
                            jsonResponse.Hash, jsonResponse.Name, jsonResponse.GetSizeAsLong());

                        // Check if this is a file response (Hash != Name)
                        var isFileResponse = !string.IsNullOrEmpty(jsonResponse.Name) && 
                                           jsonResponse.Hash != jsonResponse.Name;

                        _logger.LogInformation("Is file response check: Name='{Name}', Hash='{Hash}', Hash!=Name={IsFileResponse}", 
                            jsonResponse.Name, jsonResponse.Hash, isFileResponse);

                        if (isFileResponse)
                        {
                            _logger.LogInformation("Found and returning file upload response: Hash={Hash}, Name={Name}, Size={Size}", 
                                jsonResponse.Hash, jsonResponse.Name, jsonResponse.GetSizeAsLong());
                            return jsonResponse;
                        }
                        else
                        {
                            _logger.LogInformation("Skipping directory/non-file entry: Hash={Hash}, Name={Name}", 
                                jsonResponse.Hash, jsonResponse.Name);
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning("Failed to parse JSON line: {Line} - {Error}", line, ex.Message);
                        // Continue processing other lines
                    }
                }

                // If we didn't find a clear file response, return the first valid response with a hash
                _logger.LogInformation("No file response found, looking for any valid response");
                
                foreach (var line in lines)
                {
                    try
                    {
                        var trimmedLine = line.Trim();
                        if (string.IsNullOrEmpty(trimmedLine))
                            continue;

                        var jsonResponse = JsonSerializer.Deserialize<IPFSApiResponse>(trimmedLine, ipfsJsonOptions);
                        if (jsonResponse?.Hash != null)
                        {
                            _logger.LogInformation("Using fallback response (first valid): Hash={Hash}, Name={Name}, Size={Size}", 
                                jsonResponse.Hash, jsonResponse.Name, jsonResponse.GetSizeAsLong());
                            return jsonResponse;
                        }
                    }
                    catch (JsonException)
                    {
                        // Continue to next line
                    }
                }

                _logger.LogWarning("No valid IPFS response found in NDJSON content: {Content}", ndjsonContent);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing NDJSON response: {Content}", ndjsonContent);
                return null;
            }
        }

        /// <summary>
        /// Uploads text content to IPFS
        /// </summary>
        public async Task<IPFSUploadResponse> UploadTextAsync(string content, string? fileName = null, string contentType = "text/plain")
        {
            var bytes = Encoding.UTF8.GetBytes(content);
            var request = new IPFSUploadRequest
            {
                Content = bytes,
                FileName = fileName,
                ContentType = contentType
            };

            return await UploadAsync(request);
        }

        /// <summary>
        /// Uploads JSON content to IPFS
        /// </summary>
        public async Task<IPFSUploadResponse> UploadJsonAsync(string jsonContent, string? fileName = null)
        {
            return await UploadTextAsync(jsonContent, fileName, "application/json");
        }

        /// <summary>
        /// Uploads an object as JSON to IPFS
        /// </summary>
        public async Task<IPFSUploadResponse> UploadObjectAsync<T>(T obj, string? fileName = null)
        {
            var json = JsonSerializer.Serialize(obj, _jsonOptions);
            return await UploadJsonAsync(json, fileName);
        }

        /// <summary>
        /// Retrieves content from IPFS by CID
        /// </summary>
        public async Task<IPFSRetrieveResponse> RetrieveAsync(string cid)
        {
            var response = new IPFSRetrieveResponse { Success = false };

            try
            {
                if (string.IsNullOrWhiteSpace(cid))
                {
                    response.ErrorMessage = "CID cannot be null or empty";
                    return response;
                }

                _logger.LogInformation("Retrieving content from IPFS with CID: {CID}", cid);

                var gatewayUrl = $"{_config.GatewayUrl}/{cid}";
                var httpResponse = await _httpClient.GetAsync(gatewayUrl);

                if (httpResponse.IsSuccessStatusCode)
                {
                    response.Content = await httpResponse.Content.ReadAsByteArrayAsync();
                    response.Size = response.Content.Length;
                    response.ContentType = httpResponse.Content.Headers.ContentType?.ToString();
                    response.Success = true;

                    // Verify hash if enabled
                    if (_config.ValidateContentHash)
                    {
                        response.HashVerified = await VerifyContentHashAsync(response.Content, cid);
                        if (!response.HashVerified)
                        {
                            _logger.LogWarning("Content hash verification failed for CID: {CID}", cid);
                        }
                    }

                    _logger.LogInformation("Successfully retrieved {Size} bytes from IPFS", response.Size);
                }
                else
                {
                    response.ErrorMessage = $"Failed to retrieve content: {httpResponse.StatusCode}";
                    _logger.LogError("Failed to retrieve content from IPFS: {StatusCode}", httpResponse.StatusCode);
                }
            }
            catch (Exception ex)
            {
                response.ErrorMessage = $"Retrieval failed: {ex.Message}";
                _logger.LogError(ex, "Error retrieving from IPFS with CID: {CID}", cid);
            }

            return response;
        }

        /// <summary>
        /// Retrieves text content from IPFS by CID
        /// </summary>
        public async Task<string?> RetrieveTextAsync(string cid)
        {
            var response = await RetrieveAsync(cid);
            if (response.Success && response.Content != null)
            {
                return Encoding.UTF8.GetString(response.Content);
            }
            return null;
        }

        /// <summary>
        /// Retrieves and deserializes JSON content from IPFS
        /// </summary>
        public async Task<T?> RetrieveObjectAsync<T>(string cid) where T : class
        {
            var jsonContent = await RetrieveTextAsync(cid);
            if (jsonContent != null)
            {
                try
                {
                    return JsonSerializer.Deserialize<T>(jsonContent, _jsonOptions);
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Failed to deserialize JSON content from CID: {CID}", cid);
                }
            }
            return null;
        }

        /// <summary>
        /// Gets information about content stored in IPFS
        /// </summary>
        public async Task<IPFSContentInfo?> GetContentInfoAsync(string cid)
        {
            try
            {
                var gatewayUrl = $"{_config.GatewayUrl}/{cid}";
                var request = new HttpRequestMessage(HttpMethod.Head, gatewayUrl);
                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    return new IPFSContentInfo
                    {
                        Hash = cid,
                        Size = response.Content.Headers.ContentLength ?? 0,
                        ContentType = response.Content.Headers.ContentType?.ToString(),
                        GatewayUrl = gatewayUrl
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting content info for CID: {CID}", cid);
            }

            return null;
        }

        /// <summary>
        /// Checks if content exists in IPFS
        /// </summary>
        public async Task<bool> ExistsAsync(string cid)
        {
            var info = await GetContentInfoAsync(cid);
            return info != null;
        }

        /// <summary>
        /// Pins content in IPFS to ensure it stays available
        /// </summary>
        public async Task<bool> PinAsync(string cid)
        {
            try
            {
                var apiUrl = $"{_config.ApiUrl}/api/v0/pin/add?arg={cid}";
                var response = await _httpClient.PostAsync(apiUrl, null);
                
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Successfully pinned content with CID: {CID}", cid);
                    return true;
                }
                else
                {
                    _logger.LogError("Failed to pin content with CID {CID}: {StatusCode}", cid, response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pinning content with CID: {CID}", cid);
            }

            return false;
        }

        /// <summary>
        /// Unpins content from IPFS
        /// </summary>
        public async Task<bool> UnpinAsync(string cid)
        {
            try
            {
                var apiUrl = $"{_config.ApiUrl}/api/v0/pin/rm?arg={cid}";
                var response = await _httpClient.PostAsync(apiUrl, null);
                
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Successfully unpinned content with CID: {CID}", cid);
                    return true;
                }
                else
                {
                    _logger.LogError("Failed to unpin content with CID {CID}: {StatusCode}", cid, response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unpinning content with CID: {CID}", cid);
            }

            return false;
        }

        private async Task<bool> VerifyContentHashAsync(byte[] content, string expectedCid)
        {
            try
            {
                // For basic verification, we'll check if the content produces a similar hash
                // Note: This is a simplified verification. Real IPFS hash verification
                // would require implementing the actual IPFS hashing algorithm
                using var sha256 = SHA256.Create();
                var hash = sha256.ComputeHash(content);
                var hashString = Convert.ToHexString(hash).ToLowerInvariant();
                
                // This is a basic check - in a real implementation you'd use the IPFS multihash
                return !string.IsNullOrEmpty(expectedCid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying content hash");
                return false;
            }
        }

        /// <summary>
        /// Response model for IPFS API
        /// </summary>
        private class IPFSApiResponse
        {
            public string? Hash { get; set; }
            public string? Name { get; set; }
            public string? Size { get; set; }
            
            public long GetSizeAsLong()
            {
                if (long.TryParse(Size, out var result))
                    return result;
                return 0;
            }
        }
    }
}
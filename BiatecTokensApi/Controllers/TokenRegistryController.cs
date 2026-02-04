using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.TokenRegistry;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Controller for token registry operations
    /// </summary>
    /// <remarks>
    /// Provides endpoints for querying the token registry, retrieving token details,
    /// and triggering ingestion from external sources.
    /// Supports filtering by standard, compliance status, chain, issuer, and operational readiness.
    /// </remarks>
    [ApiController]
    [Route("api/v1/registry")]
    [Authorize]
    public class TokenRegistryController : ControllerBase
    {
        private readonly ITokenRegistryService _registryService;
        private readonly IRegistryIngestionService _ingestionService;
        private readonly ILogger<TokenRegistryController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="TokenRegistryController"/> class
        /// </summary>
        public TokenRegistryController(
            ITokenRegistryService registryService,
            IRegistryIngestionService ingestionService,
            ILogger<TokenRegistryController> logger)
        {
            _registryService = registryService;
            _ingestionService = ingestionService;
            _logger = logger;
        }

        /// <summary>
        /// Lists tokens in the registry with optional filtering and pagination
        /// </summary>
        /// <param name="request">Filter and pagination parameters</param>
        /// <returns>Paginated list of token registry entries</returns>
        /// <remarks>
        /// Example request: GET /api/v1/registry/tokens?standard=ARC3&amp;complianceStatus=Compliant&amp;page=1&amp;pageSize=20
        /// 
        /// Supports filtering by:
        /// - standard: Token standard (ASA, ARC3, ARC200, ERC20, etc.)
        /// - complianceStatus: Compliance state (Unknown, Pending, Compliant, NonCompliant, Suspended, Exempt)
        /// - chain: Blockchain network (algorand-mainnet, base-mainnet, etc.)
        /// - issuerAddress: Issuer blockchain address
        /// - isContractVerified: Whether contract is verified on block explorer
        /// - isAudited: Whether token has been audited
        /// - hasValidMetadata: Whether token has valid metadata
        /// - search: Search by name or symbol
        /// - tags: Filter by tags (comma-separated)
        /// - dataSource: Filter by data source (internal, external, etc.)
        /// 
        /// Supports sorting by:
        /// - name, symbol, createdAt, updatedAt, complianceScore, deployedAt
        /// - sortDirection: asc or desc
        /// 
        /// Response includes pagination metadata:
        /// - totalCount: Total number of matching tokens
        /// - page: Current page number
        /// - pageSize: Number of items per page
        /// - totalPages: Total number of pages
        /// - hasNextPage: Whether there is a next page
        /// - hasPreviousPage: Whether there is a previous page
        /// </remarks>
        [HttpGet("tokens")]
        [SwaggerOperation(
            Summary = "List tokens in registry",
            Description = "Returns a paginated list of tokens with optional filtering by standard, compliance status, chain, issuer, and operational readiness"
        )]
        [SwaggerResponse(200, "Successfully retrieved token list", typeof(ListTokenRegistryResponse))]
        [SwaggerResponse(400, "Invalid request parameters", typeof(ApiErrorResponse))]
        [SwaggerResponse(401, "Unauthorized - invalid or missing authentication", typeof(ApiErrorResponse))]
        [SwaggerResponse(500, "Internal server error", typeof(ApiErrorResponse))]
        public async Task<IActionResult> ListTokens([FromQuery] ListTokenRegistryRequest request)
        {
            try
            {
                _logger.LogInformation(
                    "List tokens request from {User} - Page: {Page}, PageSize: {PageSize}, Standard: {Standard}, Chain: {Chain}",
                    LoggingHelper.SanitizeLogInput(User.Identity?.Name ?? "anonymous"),
                    LoggingHelper.SanitizeLogInput(request.Page.ToString()),
                    LoggingHelper.SanitizeLogInput(request.PageSize.ToString()),
                    LoggingHelper.SanitizeLogInput(request.Standard ?? "all"),
                    LoggingHelper.SanitizeLogInput(request.Chain ?? "all")
                );

                // Validate request
                if (request.Page < 1 || request.PageSize < 1 || request.PageSize > 100)
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        ErrorCode = ErrorCodes.InvalidRequest,
                        ErrorMessage = "Invalid pagination parameters. Page must be >= 1, PageSize must be 1-100."
                    });
                }

                var response = await _registryService.ListTokensAsync(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing tokens");
                return StatusCode(500, new ApiErrorResponse
                {
                    ErrorCode = ErrorCodes.InternalError,
                    ErrorMessage = "An error occurred while listing tokens"
                });
            }
        }

        /// <summary>
        /// Gets detailed information for a single token
        /// </summary>
        /// <param name="identifier">Token identifier (registry ID, asset ID, contract address, or symbol)</param>
        /// <param name="chain">Optional chain filter to disambiguate symbols</param>
        /// <returns>Complete token details including compliance metadata and operational readiness</returns>
        /// <remarks>
        /// Example request: GET /api/v1/registry/tokens/123456 or GET /api/v1/registry/tokens/USDC?chain=algorand-mainnet
        /// 
        /// The identifier can be:
        /// - Registry ID (UUID)
        /// - Blockchain token identifier (asset ID for Algorand, contract address for EVM)
        /// - Token symbol (may require chain parameter for disambiguation)
        /// 
        /// Response includes:
        /// - Token identity (name, symbol, decimals, total supply)
        /// - Supported standards
        /// - Issuer identity (name, address, verification status)
        /// - Compliance scoring (status, score, regulatory frameworks, jurisdictions)
        /// - Operational readiness (contract verification, audits, metadata validity)
        /// - External registries and tags
        /// - Creation and update timestamps
        /// 
        /// Missing data is represented explicitly with null values rather than omitted.
        /// </remarks>
        [HttpGet("tokens/{identifier}")]
        [SwaggerOperation(
            Summary = "Get token details",
            Description = "Returns complete details for a single token including all compliance metadata and operational readiness fields"
        )]
        [SwaggerResponse(200, "Successfully retrieved token details", typeof(GetTokenRegistryResponse))]
        [SwaggerResponse(404, "Token not found", typeof(ApiErrorResponse))]
        [SwaggerResponse(401, "Unauthorized - invalid or missing authentication", typeof(ApiErrorResponse))]
        [SwaggerResponse(500, "Internal server error", typeof(ApiErrorResponse))]
        public async Task<IActionResult> GetToken(string identifier, [FromQuery] string? chain = null)
        {
            try
            {
                _logger.LogInformation(
                    "Get token request from {User} - Identifier: {Identifier}, Chain: {Chain}",
                    LoggingHelper.SanitizeLogInput(User.Identity?.Name ?? "anonymous"),
                    LoggingHelper.SanitizeLogInput(identifier),
                    LoggingHelper.SanitizeLogInput(chain ?? "any")
                );

                var response = await _registryService.GetTokenAsync(identifier, chain);

                if (!response.Found)
                {
                    return NotFound(new ApiErrorResponse
                    {
                        ErrorCode = ErrorCodes.NotFound,
                        ErrorMessage = response.ErrorMessage ?? "Token not found"
                    });
                }

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting token {Identifier}", LoggingHelper.SanitizeLogInput(identifier));
                return StatusCode(500, new ApiErrorResponse
                {
                    ErrorCode = ErrorCodes.InternalError,
                    ErrorMessage = "An error occurred while retrieving token details"
                });
            }
        }

        /// <summary>
        /// Creates or updates a token in the registry
        /// </summary>
        /// <param name="request">Token data</param>
        /// <returns>Upsert result with registry ID and created/updated flag</returns>
        /// <remarks>
        /// Example request: POST /api/v1/registry/tokens
        /// {
        ///   "tokenIdentifier": "123456",
        ///   "chain": "algorand-mainnet",
        ///   "name": "My Token",
        ///   "symbol": "MTK",
        ///   "decimals": 6,
        ///   "totalSupply": "1000000",
        ///   "supportedStandards": ["ASA", "ARC3"],
        ///   "primaryStandard": "ARC3",
        ///   "issuer": {
        ///     "name": "My Company",
        ///     "address": "ABCD...",
        ///     "isVerified": true
        ///   },
        ///   "compliance": {
        ///     "status": "Compliant",
        ///     "regulatoryFrameworks": ["MICA"],
        ///     "jurisdictions": ["EU"]
        ///   },
        ///   "readiness": {
        ///     "isContractVerified": true,
        ///     "isAudited": true,
        ///     "hasValidMetadata": true
        ///   },
        ///   "dataSource": "internal"
        /// }
        /// 
        /// Performs idempotent upsert based on tokenIdentifier and chain combination.
        /// If a token with the same identifier and chain exists, it will be updated.
        /// Otherwise, a new entry will be created.
        /// 
        /// Response indicates whether a new entry was created or an existing entry was updated.
        /// </remarks>
        [HttpPost("tokens")]
        [SwaggerOperation(
            Summary = "Create or update token",
            Description = "Creates a new token registry entry or updates an existing one. Idempotent based on tokenIdentifier and chain."
        )]
        [SwaggerResponse(200, "Successfully upserted token", typeof(UpsertTokenRegistryResponse))]
        [SwaggerResponse(400, "Invalid request or validation failed", typeof(ApiErrorResponse))]
        [SwaggerResponse(401, "Unauthorized - invalid or missing authentication", typeof(ApiErrorResponse))]
        [SwaggerResponse(500, "Internal server error", typeof(ApiErrorResponse))]
        public async Task<IActionResult> UpsertToken([FromBody] UpsertTokenRegistryRequest request)
        {
            try
            {
                var userAddress = User.Identity?.Name;

                _logger.LogInformation(
                    "Upsert token request from {User} - Symbol: {Symbol}, Identifier: {TokenIdentifier}, Chain: {Chain}",
                    LoggingHelper.SanitizeLogInput(userAddress ?? "anonymous"),
                    LoggingHelper.SanitizeLogInput(request.Symbol),
                    LoggingHelper.SanitizeLogInput(request.TokenIdentifier),
                    LoggingHelper.SanitizeLogInput(request.Chain)
                );

                var response = await _registryService.UpsertTokenAsync(request, userAddress);

                if (!response.Success)
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        ErrorCode = ErrorCodes.ValidationFailed,
                        ErrorMessage = response.ErrorMessage ?? "Failed to upsert token"
                    });
                }

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error upserting token {Symbol}", LoggingHelper.SanitizeLogInput(request.Symbol));
                return StatusCode(500, new ApiErrorResponse
                {
                    ErrorCode = ErrorCodes.InternalError,
                    ErrorMessage = "An error occurred while upserting the token"
                });
            }
        }

        /// <summary>
        /// Triggers manual ingestion of token data from specified sources
        /// </summary>
        /// <param name="request">Ingestion parameters</param>
        /// <returns>Ingestion result with statistics and anomalies</returns>
        /// <remarks>
        /// Example request: POST /api/v1/registry/ingest
        /// {
        ///   "source": "internal",
        ///   "chain": "algorand-mainnet",
        ///   "force": false,
        ///   "limit": 100
        /// }
        /// 
        /// Supported sources:
        /// - "internal": Ingest from internal token deployment records
        /// - "all": Ingest from all available sources
        /// 
        /// The force flag allows re-ingestion of existing entries.
        /// The limit parameter restricts the number of entries processed (useful for testing).
        /// 
        /// Response includes:
        /// - processedCount: Total number of entries processed
        /// - createdCount: Number of new entries created
        /// - updatedCount: Number of existing entries updated
        /// - skippedCount: Number of entries skipped due to validation failures
        /// - errorCount: Number of errors encountered
        /// - anomalies: List of warnings or anomalies detected
        /// - errors: List of error messages
        /// - duration: Time taken for ingestion
        /// 
        /// Ingestion is idempotent - running it multiple times will not create duplicates.
        /// Anomalies are logged for review but do not stop the ingestion process.
        /// </remarks>
        [HttpPost("ingest")]
        [SwaggerOperation(
            Summary = "Trigger registry ingestion",
            Description = "Manually triggers ingestion of token data from internal or external sources with idempotent processing"
        )]
        [SwaggerResponse(200, "Ingestion completed successfully", typeof(IngestRegistryResponse))]
        [SwaggerResponse(400, "Invalid ingestion parameters", typeof(ApiErrorResponse))]
        [SwaggerResponse(401, "Unauthorized - invalid or missing authentication", typeof(ApiErrorResponse))]
        [SwaggerResponse(500, "Internal server error", typeof(ApiErrorResponse))]
        public async Task<IActionResult> IngestRegistry([FromBody] IngestRegistryRequest request)
        {
            try
            {
                _logger.LogInformation(
                    "Ingest registry request from {User} - Source: {Source}, Chain: {Chain}, Force: {Force}, Limit: {Limit}",
                    LoggingHelper.SanitizeLogInput(User.Identity?.Name ?? "anonymous"),
                    LoggingHelper.SanitizeLogInput(request.Source),
                    LoggingHelper.SanitizeLogInput(request.Chain ?? "all"),
                    LoggingHelper.SanitizeLogInput(request.Force.ToString()),
                    LoggingHelper.SanitizeLogInput(request.Limit?.ToString() ?? "unlimited")
                );

                var response = await _ingestionService.IngestAsync(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during registry ingestion");
                return StatusCode(500, new ApiErrorResponse
                {
                    ErrorCode = ErrorCodes.InternalError,
                    ErrorMessage = "An error occurred during registry ingestion"
                });
            }
        }

        /// <summary>
        /// Searches for tokens by name or symbol
        /// </summary>
        /// <param name="q">Search query</param>
        /// <param name="limit">Maximum number of results (default: 10, max: 50)</param>
        /// <returns>List of matching tokens</returns>
        /// <remarks>
        /// Example request: GET /api/v1/registry/search?q=USDC&amp;limit=10
        /// 
        /// Searches across:
        /// - Token name
        /// - Token symbol
        /// - Token identifier (asset ID/contract address)
        /// 
        /// Returns a simplified list suitable for autocomplete or quick search.
        /// For more detailed filtering, use the /tokens endpoint.
        /// </remarks>
        [HttpGet("search")]
        [SwaggerOperation(
            Summary = "Search tokens",
            Description = "Searches for tokens by name, symbol, or identifier"
        )]
        [SwaggerResponse(200, "Successfully retrieved search results", typeof(List<TokenRegistryEntry>))]
        [SwaggerResponse(400, "Invalid search parameters", typeof(ApiErrorResponse))]
        [SwaggerResponse(401, "Unauthorized - invalid or missing authentication", typeof(ApiErrorResponse))]
        [SwaggerResponse(500, "Internal server error", typeof(ApiErrorResponse))]
        public async Task<IActionResult> SearchTokens([FromQuery] string q, [FromQuery] int limit = 10)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(q))
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        ErrorCode = ErrorCodes.InvalidRequest,
                        ErrorMessage = "Search query (q) is required"
                    });
                }

                if (limit < 1 || limit > 50)
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        ErrorCode = ErrorCodes.InvalidRequest,
                        ErrorMessage = "Limit must be between 1 and 50"
                    });
                }

                _logger.LogInformation(
                    "Search tokens request from {User} - Query: {Query}, Limit: {Limit}",
                    LoggingHelper.SanitizeLogInput(User.Identity?.Name ?? "anonymous"),
                    LoggingHelper.SanitizeLogInput(q),
                    LoggingHelper.SanitizeLogInput(limit.ToString())
                );

                var results = await _registryService.SearchTokensAsync(q, limit);
                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching tokens with query {Query}", LoggingHelper.SanitizeLogInput(q));
                return StatusCode(500, new ApiErrorResponse
                {
                    ErrorCode = ErrorCodes.InternalError,
                    ErrorMessage = "An error occurred while searching tokens"
                });
            }
        }
    }
}

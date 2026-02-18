# GitHub Copilot Instructions for BiatecTokensApi

## CRITICAL: Understanding "Vision-Driven" vs "Implementation" Issues

**BEFORE starting any issue labeled "vision-driven" or containing extensive user stories:**

1. **Read the ENTIRE issue carefully** - Look for these key phrases that indicate IMPLEMENTATION work, not just verification:
   - "Implement a hardening program"
   - "Replace legacy or ambiguous behavior"
   - "Add deterministic validation checks" 
   - "Remove or refactor brittle test behaviors"
   - "Add and stabilize automated tests"

2. **Distinguish between two types of requests:**
   - **Verification Request**: "Verify that X works", "Document that Y exists", "Confirm Z is implemented"
     - Response: Run tests, document existing capabilities, create verification reports
   - **Implementation Request**: "Implement X", "Add Y", "Replace Z with", "Refactor A"
     - Response: Write new code, add new tests, modify existing code

3. **When in doubt, check existing test coverage FIRST**:
   ```bash
   # Check if tests exist for the requested feature
   find BiatecTokensTests -name "*FeatureName*"
   dotnet test --list-tests | grep "FeatureName"
   ```

4. **If tests already exist and pass (100%), ask the product owner for clarification:**
   - "The requested tests already exist in [file]. What additional coverage is needed?"
   - Do NOT assume documentation-only is sufficient if the issue says "implement" or "add"

5. **Product Roadmap Alignment**:
   - Always check https://raw.githubusercontent.com/scholtz/biatec-tokens/refs/heads/main/business-owner-roadmap.md
   - Understand if the feature is "Not Started" (needs implementation) vs "Partial" (needs completion)
   - Align your work with the roadmap status percentages

**Lesson Learned (2026-02-18)**: Issue titled "Vision-driven next step: harden deterministic ARC76 orchestration" was misinterpreted as a verification task when it explicitly requested implementation work ("Implement a hardening program", "Replace legacy behavior", "Add validation checks"). The issue had extensive user stories which made it appear like a documentation request, but the acceptance criteria required actual code changes and new tests. Always read the "In Scope" section carefully - it explicitly lists what needs to be implemented vs verified.

## Project Overview

BiatecTokensApi is a comprehensive .NET 8.0 Web API for deploying and managing various types of tokens on different blockchain networks, including ERC20 tokens on EVM chains (Base blockchain) and multiple Algorand token standards (ASA, ARC3, ARC200).

## Technology Stack

- **Framework**: .NET 10.0 (C#)
- **IDE**: Visual Studio 2022 or Visual Studio Code
- **Package Manager**: NuGet
- **Testing Framework**: NUnit (not xUnit)
- **API Documentation**: Swagger/OpenAPI (Swashbuckle)
- **Blockchain Libraries**:
  - Algorand4 (v4.4.1.2026010317) - Algorand blockchain integration
  - Nethereum.Web3 (v5.8.0) - Ethereum/EVM blockchain integration
  - AlgorandAuthentication (v2.1.1) - ARC-0014 authentication
  - AlgorandARC76Account (v1.1.0) - ARC-76 account management
- **Containerization**: Docker

## Build, Test, and Run Commands

### Build Commands
```bash
# Restore dependencies
dotnet restore

# Build the solution
dotnet build BiatecTokensApi.sln

# Build in Release mode
dotnet build BiatecTokensApi.sln --configuration Release

# Build specific project
dotnet build BiatecTokensApi/BiatecTokensApi.csproj
```

### Test Commands
```bash
# Run all tests
dotnet test BiatecTokensTests

# Run tests with detailed output
dotnet test BiatecTokensTests --verbosity detailed

# Run specific test
dotnet test BiatecTokensTests --filter "FullyQualifiedName~TokenServiceTests"
```

### Run Commands
```bash
# Run the API locally
dotnet run --project BiatecTokensApi/BiatecTokensApi.csproj

# Access Swagger documentation at https://localhost:7000/swagger
```

### Docker Commands
```bash
# Build Docker image
docker build -t biatec-tokens-api -f BiatecTokensApi/Dockerfile .

# Run Docker container
docker run -p 7000:7000 biatec-tokens-api
```

## Project Structure

```
BiatecTokensApi/
├── BiatecTokensApi/          # Main API project
│   ├── ABI/                  # Smart contract ABIs
│   ├── Configuration/        # Configuration models
│   ├── Controllers/          # API controllers
│   ├── Generated/            # Auto-generated client code
│   ├── Models/               # Data models and DTOs
│   ├── Properties/           # Project properties
│   ├── Repositories/         # Data access layer
│   ├── Services/             # Business logic
│   │   └── Interface/        # Service interfaces
│   ├── doc/                  # XML documentation
│   ├── Program.cs            # Application entry point
│   └── appsettings.json      # Configuration settings
├── BiatecTokensTests/        # Test project
├── k8s/                      # Kubernetes configurations
└── BiatecTokensApi.sln       # Solution file
```

## Code Style and Conventions

### General C# Conventions
- Follow standard C# naming conventions (PascalCase for public members, camelCase for private fields)
- Use explicit types instead of `var` when type is not obvious
- Enable nullable reference types (`<Nullable>enable</Nullable>`)
- Use implicit usings (`<ImplicitUsings>enable</ImplicitUsings>`)
- Add XML documentation comments for public APIs
- Include documentation XML file in Debug builds

### Namespace Conventions
- Use the project name as the root namespace: `BiatecTokensApi`
- Organize code by feature: Controllers, Models, Services, Repositories

### Method Documentation
- Always add XML documentation (`///`) for public methods, classes, and properties
- Include `<summary>`, `<param>`, `<returns>`, and `<exception>` tags as appropriate
- Documentation file is generated at `doc/documentation.xml`

### Example Documentation Style
```csharp
/// <summary>
/// Creates a new ERC20 mintable token on the specified EVM chain.
/// </summary>
/// <param name="request">The token creation request containing token parameters.</param>
/// <returns>The transaction result including asset ID and transaction hash.</returns>
/// <exception cref="ArgumentNullException">Thrown when request is null.</exception>
public async Task<TokenCreationResponse> CreateERC20MintableAsync(CreateERC20MintableRequest request)
```

## Testing Practices

### Test Naming Convention
- Use descriptive test names that explain what is being tested
- Follow pattern: `MethodName_Scenario_ExpectedResult`
- Example: `CreateToken_ValidRequest_ReturnsSuccess`

### Test Structure
- Use AAA pattern: Arrange, Act, Assert
- Keep tests focused and test one thing at a time
- Use meaningful test data that represents real scenarios
- Mock external dependencies (blockchain calls, IPFS)

### Test Categories
- Unit tests for service logic
- Integration tests for repository interactions
- Controller tests for API endpoints
- Use `TestHelper` class for common test utilities

### Integration Test Configuration
**CRITICAL**: When adding new required configuration to core services (e.g., new DI services, required configuration sections), **ALWAYS** update integration test setups.

Integration tests use `WebApplicationFactory` to instantiate the full application stack, requiring complete configuration.

**Common Integration Test Files Requiring Updates**:
- `JwtAuthTokenDeploymentIntegrationTests.cs`
- `ARC76CredentialDerivationTests.cs`
- `ARC76EdgeCaseAndNegativeTests.cs`
- Any test file that creates `WebApplicationFactory<Program>`

**Example: Adding New Configuration**
```csharp
[SetUp]
public void Setup()
{
    var configuration = new Dictionary<string, string?>
    {
        // Existing configs...
        ["JwtConfig:SecretKey"] = "test-secret-key...",
        
        // NEW: Add your required configuration here
        ["KeyManagementConfig:Provider"] = "Hardcoded",
        ["KeyManagementConfig:HardcodedKey"] = "TestKeyForIntegrationTests32CharactersMinimumRequired"
    };
    
    _factory = new WebApplicationFactory<Program>()
        .WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(configuration);
            });
        });
}
```

**Integration Test Best Practices**:
- Use `Hardcoded` provider for test-specific configuration (e.g., KeyManagementConfig)
- Ensure test keys/secrets meet minimum requirements (e.g., 32+ characters)
- Never use production keys in test configurations
- Always run full test suite after adding new required services: `dotnet test --configuration Release --no-build`
- Check for test failures related to missing configuration (BadRequest errors often indicate config issues)

**Lesson Learned (2026-02-09)**: Adding KeyManagementConfig to AuthenticationService caused 18 integration test failures because test setups didn't include the new required configuration. Always audit integration test configs when adding new required services.

### End-to-End (E2E) Testing Best Practices

**CRITICAL**: E2E tests must be self-contained and avoid dependencies on complex external services or configuration.

#### E2E Test Principles

1. **Minimize External Dependencies**
   - Avoid calling endpoints that require Stripe, KYC, or other third-party service configuration
   - Focus on core application logic that can be tested with mock configuration
   - If an endpoint requires complex setup, either mock it properly or test a simpler alternative flow

2. **Test What You Control**
   - ✅ **GOOD**: Test auth flow (register → login → token refresh)
   - ✅ **GOOD**: Test ARC76 determinism (same credentials → same address)
   - ✅ **GOOD**: Test error handling for invalid inputs
   - ❌ **BAD**: Test endpoints requiring external API keys not in test config
   - ❌ **BAD**: Test flows dependent on third-party service responses
   - ❌ **BAD**: Test features requiring production-only configuration

3. **When a Test Fails in CI**
   - First check: Does the test rely on services not properly mocked?
   - Check if the endpoint has `[Authorize]` attribute - does the test properly authenticate?
   - Check if the endpoint requires specific configuration (Stripe, KYC, etc.)
   - **Solution**: Replace complex endpoint calls with simpler alternatives that test the same business logic

4. **Prefer Token-Based Auth Tests Over Complex JWT Flows**
   - Simple auth tests: Register, login, verify determinism
   - Token lifecycle tests: Refresh tokens, token expiration
   - **Avoid**: Complex authorization flows requiring multiple service integrations

#### E2E Test Template (JWT Authentication)

```csharp
[Test]
[NonParallelizable]
public async Task E2E_AuthFlow_ShouldWork()
{
    // 1. Register user
    var registerRequest = new RegisterRequest
    {
        Email = $"test-{Guid.NewGuid()}@example.com",
        Password = "SecurePass123!",
        ConfirmPassword = "SecurePass123!"
    };
    var registerResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);
    var registerResult = await registerResponse.Content.ReadFromJsonAsync<RegisterResponse>();
    
    Assert.That(registerResult!.AlgorandAddress, Is.Not.Null);
    var address = registerResult.AlgorandAddress;
    
    // 2. Login - verify determinism
    var loginRequest = new LoginRequest { Email = registerRequest.Email, Password = registerRequest.Password };
    var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);
    var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
    
    Assert.That(loginResult!.AlgorandAddress, Is.EqualTo(address), "ARC76 address must be deterministic");
    
    // 3. Test additional auth features WITHOUT external dependencies
    // e.g., token refresh, JWT structure validation
}
```

#### Common E2E Testing Mistakes and Solutions

**Mistake #1: Testing endpoints with complex authorization requirements**
```csharp
// ❌ BAD: Calls preflight endpoint requiring Stripe subscription service
var preflightResponse = await _client.PostAsJsonAsync("/api/v1/preflight", request);
// This fails with 401 Unauthorized because Stripe service isn't properly mocked
```

**Solution**: Test simpler alternative that validates same business logic
```csharp
// ✅ GOOD: Test token refresh flow instead
var refreshRequest = new { RefreshToken = refreshToken };
var refreshResponse = await _client.PostAsJsonAsync("/api/v1/auth/refresh", refreshRequest);
// Tests JWT lifecycle without external dependencies
```

**Mistake #2: Not checking endpoint requirements before testing**
- Always check if endpoint has `[Authorize]` attribute
- Check controller dependencies (IStripeService, IKycService, etc.)
- Check required configuration in appsettings

**Mistake #3: Assuming test configuration equals production configuration**
- Test configs use mocks and hardcoded values
- Some endpoints expect real service credentials
- **Solution**: Test endpoints that work with mock configuration

#### Lesson Learned (2026-02-18)

**Issue**: E2E test called `/api/v1/preflight` endpoint which requires:
- JWT authentication (properly configured ✅)
- Stripe subscription service (not mocked in test ❌)
- KYC service configuration (not mocked in test ❌)  
- Entitlement evaluation services (complex dependencies ❌)

**Root Cause**: Test attempted to validate too much in a single E2E flow, creating brittle dependencies on external services.

**Fix**: Replaced complex preflight test with focused token refresh test:
- Tests JWT lifecycle (register → login → refresh)
- Validates ARC76 determinism (same address across sessions)
- Verifies token structure (3-part JWT format)
- **Result**: 100% test pass rate, no external dependencies

**Key Takeaway**: E2E tests should validate critical paths using the simplest possible implementation. If a test requires complex mocking of external services, it's a sign the test is too broad. Break it into focused unit/integration tests instead.

## Authentication and Security

### ARC-0014 Algorand Authentication
- All API endpoints require ARC-0014 authentication
- Realm: `BiatecTokens#ARC14`
- Authorization header format: `Authorization: SigTx <signed-transaction>`
- Check expiration is enabled by default
- Validate signatures against configured Algorand networks

### Security Best Practices
- **NEVER** commit sensitive data (mnemonics, private keys, API keys)
- Use `appsettings.json` for configuration templates only
- Use User Secrets for local development: `dotnet user-secrets set "App:Account" "your-mnemonic"`
- Use environment variables for production deployments
- Validate all input parameters before processing
- Use proper error handling to avoid leaking sensitive information
- **MANDATORY: ALWAYS sanitize all user-provided inputs before logging to prevent log forging attacks and CodeQL security warnings**
- Create utility methods for input sanitization and use them consistently across the codebase

### Logging Security
- **CRITICAL: Never log raw user input directly. Always use `LoggingHelper.SanitizeLogInput()` for any user-provided value in logs**
- This prevents CodeQL "Log entries created from user input" high severity vulnerabilities
- Use the `LoggingHelper` utility class for consistent sanitization across the codebase
- Control characters and excessively long inputs are automatically filtered
- Apply to all logging levels: LogInformation, LogWarning, LogError, LogDebug

**Example of INCORRECT logging (will trigger CodeQL):**
```csharp
_logger.LogInformation("User {UserId} requested {Action}", userId, action); // BAD: userId and action are raw user inputs
```

**Example of CORRECT logging:**
```csharp
_logger.LogInformation("User {UserId} requested {Action}", 
    LoggingHelper.SanitizeLogInput(userId), 
    LoggingHelper.SanitizeLogInput(action)); // GOOD: sanitized
```

- For multiple values, use `LoggingHelper.SanitizeLogInputs()` or sanitize individually
- Always sanitize before logging, even in debug logs

### Idempotency Handling
- When implementing idempotency for sensitive operations (like exports), always validate that cached requests match current requests
- Store the full request parameters in cache, not just the response
- Check request equivalence before returning cached results
- Log warnings when idempotency keys are reused with different parameters
- Prevent bypass of business logic through mismatched cached responses

### Secrets Management
```bash
# Set user secret for local development
dotnet user-secrets set "App:Account" "your-mnemonic-phrase"
dotnet user-secrets set "IPFSConfig:Username" "your-ipfs-username"
dotnet user-secrets set "IPFSConfig:Password" "your-ipfs-password"
```

## Blockchain-Specific Guidelines

### Algorand Token Standards
- **ASA (Algorand Standard Assets)**: Basic fungible tokens, NFTs, and fractional NFTs
- **ARC3**: Tokens with rich metadata stored on IPFS
- **ARC200**: Advanced smart contract tokens with mintable capabilities

### EVM Token Standards
- **ERC20**: Fungible tokens on Base blockchain (Chain ID: 8453)
- Support both mintable (with cap) and preminted (fixed supply) variants

### Transaction Handling
- Always validate network configuration before submitting transactions
- Use appropriate gas limits for EVM transactions (default: 4,500,000)
- Monitor transaction confirmation on blockchain
- Return comprehensive transaction results including transaction ID and confirmed round

### Network Configuration
- Support multiple Algorand networks: mainnet, testnet, betanet, voimain, aramidmain
- Each network requires separate configuration in `AlgorandAuthentication.AllowedNetworks`
- EVM networks configured in `EVMChains` array

## IPFS Integration

### Configuration
- IPFS used for storing ARC3 token metadata
- Configure API URL, gateway URL, credentials in `IPFSConfig`
- Default timeout: 30 seconds
- Max file size: 10 MB (10485760 bytes)
- Content hash validation enabled by default

### Best Practices
- Always validate content hash after upload
- Handle timeout errors gracefully
- Use appropriate error messages for IPFS failures
- Test IPFS connectivity before deploying metadata

## API Design

### Controller Patterns
- Use attribute routing: `[Route("api/v1/token")]`
- Use HTTP verb attributes: `[HttpPost]`, `[HttpGet]`, etc.
- Return consistent response format: `TokenCreationResponse`
- Include proper status codes (200, 400, 401, 403, 500)
- Use `[Authorize]` attribute for authenticated endpoints

### Response Format
All endpoints return a consistent response structure:
```csharp
public class TokenCreationResponse
{
    public bool Success { get; set; }
    public string? TransactionId { get; set; }
    public ulong? AssetId { get; set; }
    public string? CreatorAddress { get; set; }
    public ulong? ConfirmedRound { get; set; }
    public string? ErrorMessage { get; set; }
}
```

### Error Handling
- Catch specific exceptions at the appropriate level
- Log errors with appropriate context
- Return user-friendly error messages
- Never expose internal implementation details in error responses
- Include validation errors in response

## Service Layer

### Dependency Injection
- Register services in `Program.cs`
- Use interface-based design for testability
- Prefer constructor injection
- Use `IServiceProvider` sparingly

### Service Responsibilities
- Services contain business logic
- Services coordinate between repositories and controllers
- Services handle blockchain interactions
- Services validate business rules

## Repository Layer

### Data Access Patterns
- Repositories handle IPFS interactions
- Use async/await for all I/O operations
- Implement proper retry logic for network operations
- Handle timeouts and network errors gracefully

## Deployment

### CI/CD Pipeline
- Deployment triggered on push to `master` branch
- Uses SSH for deployment to staging server
- Deployment script: `deploy.sh`
- GitHub Actions workflow: `.github/workflows/build-api.yml`

### Docker Deployment
- Dockerfile located in `BiatecTokensApi/Dockerfile`
- Target OS: Linux
- Default port: 7000
- Use docker-compose for orchestration (see `compose.sh`)

### Kubernetes Deployment
- Kubernetes configurations in `k8s/` directory
- Follow k8s manifest structure for deployments, services, and ingress

## Client Code Generation

### AVM Client Generator
```bash
cd BiatecTokensApi/Generated
docker run --rm -v ".:/app/out" scholtz2/dotnet-avm-generated-client:latest \
  dotnet client-generator.dll \
  --namespace "BiatecTokensApi.Generated" \
  --url https://raw.githubusercontent.com/scholtz/arc-1400/refs/heads/main/projects/arc-1400/smart_contracts/artifacts/security_token/Arc1644.arc56.json
```

- Generated code goes in `BiatecTokensApi/Generated/` folder
- Do not modify generated code manually
- Regenerate when smart contract ABI changes

## File Management

### Files to NOT Commit
- `appsettings.Development.json` (if contains secrets)
- User secrets
- Build artifacts (`bin/`, `obj/`)
- Docker volumes
- IDE-specific files (`.vs/`, `.vscode/` user settings)
- Log files
- Temporary files

### Files to Always Commit
- Source code (`.cs` files)
- Project files (`.csproj`, `.sln`)
- Configuration templates (`appsettings.json`)
- Documentation (`README.md`, XML docs)
- Docker configurations (`Dockerfile`, `compose.sh`)
- Kubernetes manifests (`k8s/`)
- ABI files (`ABI/*.json`)
- GitHub workflows (`.github/workflows/`)

## Dependencies

### Adding New Dependencies
- Use stable versions of packages
- Review security advisories before adding packages
- Document why a dependency is needed
- Keep dependencies up to date
- Test thoroughly after updating major versions

### Key Dependencies
- **Algorand4**: Core Algorand SDK
- **Nethereum.Web3**: Ethereum interaction
- **AlgorandAuthentication**: ARC-0014 auth implementation
- **Swashbuckle.AspNetCore**: OpenAPI/Swagger documentation

## Common Tasks

### Adding a New Token Type
1. Create request/response models in `Models/`
2. Add service method in appropriate service class
3. Add controller endpoint in `Controllers/TokenController.cs`
4. Add Swagger annotations
5. Write unit tests in `BiatecTokensTests/`
6. Update README.md with endpoint documentation
7. Test with real blockchain networks

### Adding a New Blockchain Network
1. Update configuration model in `Configuration/`
2. Add network configuration to `appsettings.json`
3. Update authentication configuration if needed
4. Add network validation logic
5. Update documentation
6. Test connectivity and transactions

## Important Notes

### What NOT to Do
- Do not modify generated code in `Generated/` folder
- Do not commit secrets or private keys
- Do not skip input validation on API endpoints
- Do not ignore blockchain transaction errors
- Do not remove existing tests without replacement
- Do not change ABI files without regenerating clients
- Do not deploy to production without testing on testnet first

### What to ALWAYS Do
- Always add XML documentation for public APIs
- Always validate input parameters
- Always handle async operations properly
- Always test on testnet before mainnet
- Always use proper error handling
- Always check blockchain transaction confirmation
- Always validate authentication tokens
- Always log important operations
- Always review security implications of changes

## Support and Resources

- API Documentation: Available at `/swagger` endpoint
- Repository: https://github.com/scholtz/BiatecTokensApi
- Algorand Documentation: https://developer.algorand.org
- Nethereum Documentation: https://docs.nethereum.com
- ARC Standards: https://github.com/algorandfoundation/ARCs

## Dependency Updates and Verification

### Handling Dependabot PRs
When Dependabot creates PRs for dependency updates, follow this verification process:

#### 1. Local Verification (ALWAYS Required)
```bash
# Step 1: Restore dependencies
dotnet restore

# Step 2: Build in Release mode
dotnet build --configuration Release --no-restore

# Step 3: Run tests (excluding RealEndpoint tests)
dotnet test --configuration Release --no-build --verbosity normal --filter "FullyQualifiedName!~RealEndpoint"

# Step 4: Check for security vulnerabilities
dotnet list package --vulnerable
```

#### 2. CI Workflow Considerations
- Dependabot PRs run with **read-only permissions** by default for security
- Workflows that post comments to PRs will fail with `403 Resource not accessible by integration`
- This is **NOT a code failure** - it's expected behavior for dependabot PRs
- Always verify test results, not workflow comment step results

#### 3. Dependency Update Checklist
Before approving a dependency update PR:
- [ ] Local build succeeds with 0 errors
- [ ] All tests pass locally (verify count matches baseline: ~1397 tests)
- [ ] No new security vulnerabilities introduced
- [ ] Breaking changes documented if major version updates
- [ ] Test coverage remains at or above baseline (~99%)
- [ ] CI workflow completes (ignore comment step failures for dependabot PRs)

#### 4. Known Safe Update Types
These updates are generally safe and require only verification:
- **Patch updates** (x.y.Z): Bug fixes, security patches
- **Minor updates** (x.Y.z): New features, backward compatible
- **Framework updates**: .NET SDK patches within same major version

These require extra scrutiny:
- **Major updates** (X.y.z): May contain breaking changes
- **Multi-package updates**: Verify compatibility between updated packages
- **Security-critical packages**: JWT, authentication, cryptography libraries

#### 5. When CI Shows "Failed" for Dependabot PRs
If the CI workflow shows as "failed" for a dependabot PR:

1. **Check the actual failure reason** using GitHub Actions logs
2. **If the failure is permissions-related** (`403 Resource not accessible by integration`):
   - This is expected for dependabot PRs
   - Verify tests passed in earlier steps of the workflow
   - Verify locally as per step 1 above
3. **If the failure is test or build related**:
   - Investigate the specific error
   - Check for breaking changes in updated packages
   - Consider rejecting the PR or requesting manual updates

#### 6. Workflow Permissions
The `test-pr.yml` workflow includes these permissions:
```yaml
permissions:
  contents: read
  pull-requests: write
  issues: write
  checks: write
```

For dependabot PRs, the comment step is skipped:
```yaml
if: github.event_name == 'pull_request' && github.actor != 'dependabot[bot]'
```

#### 7. Security Advisory Checks
Before approving dependency updates, always check:
- GitHub Security Advisories for the package
- Release notes for security fixes
- Known vulnerabilities in old vs. new version

Use the `gh-advisory-database` tool for supported ecosystems before adding new dependencies.

## CI/CD Configuration Requirements

### Critical: Adding New Required Services

**ALWAYS update CI workflows when adding new required configuration sections.**

When adding new services or configuration that are required for application startup:

1. **Update `.github/workflows/test-pr.yml`** - Add configuration to the OpenAPI generation appsettings (lines 134-161)
2. **Update integration test setups** - Add configuration to WebApplicationFactory setups
3. **Test locally first** - Verify builds and tests pass with new configuration

#### Example: KeyManagementConfig Added

When KeyManagementConfig was added as a required service:
- ❌ **Initial mistake**: Forgot to add to CI workflow appsettings
- ✅ **Fix**: Added KeyManagementConfig to test-pr.yml appsettings.OpenAPI.json:
  ```json
  "KeyManagementConfig": {
    "Provider": "Hardcoded",
    "HardcodedKey": "TestKeyForCIOpenAPIGenerationOnly32CharactersMinimum"
  }
  ```

#### CI Configuration Checklist

When adding new required configuration:
- [ ] Update `.github/workflows/test-pr.yml` OpenAPI appsettings
- [ ] Update integration test WebApplicationFactory configs
- [ ] Verify `dotnet build --configuration Release` succeeds locally
- [ ] Verify `dotnet test --filter "FullyQualifiedName!~RealEndpoint"` passes
- [ ] Check OpenAPI generation doesn't fail: `swagger tofile --output ./openapi.json BiatecTokensApi/bin/Release/net10.0/BiatecTokensApi.dll v1`

**Lesson Learned**: Missing required configuration in CI causes build failures that are hard to debug. Always add new required config to ALL test and CI configurations immediately when introducing the requirement.

### WebApplicationFactory Integration Test Reliability

**CRITICAL: Integration tests using WebApplicationFactory require multiple reliability measures for CI environments.**

CI environments have resource constraints that don't affect local development. Apply ALL of these patterns:

#### Required Patterns (ALL must be used):

1. **NonParallelizable Attribute**
   ```csharp
   [NonParallelizable]
   public class MyIntegrationTests
   ```
   Prevents port conflicts and resource contention between WebApplicationFactory instances.

2. **Complete Configuration**
   - Include ALL required config sections even if test focuses on specific feature
   - Copy template from existing integration tests (e.g., `HealthCheckIntegrationTests.cs`)
   - Must include: AlgorandAuthentication, IPFS, EVM chains, CORS, Debug flags

3. **Retry Logic for Health Checks**
   ```csharp
   private async Task<HttpResponseMessage> GetHealthWithRetryAsync(
       HttpClient client, 
       int maxRetries = 10,  // Increased for CI robustness
       int delayMs = 2000)   // Longer delays for resource-constrained environments
   {
       // Implementation with exception tracking and detailed error messages
   }
   ```
   - Minimum 10 retries with 2-second delays
   - Total max wait: ~20 seconds
   - Capture and report last exception for debugging

4. **Test Configuration Template**
   ```csharp
   var configuration = new Dictionary<string, string?>
   {
       ["KeyManagementConfig:Provider"] = "Hardcoded",
       ["KeyManagementConfig:HardcodedKey"] = "TestKey32CharactersMinimumLength",
       ["AlgorandAuthentication:Realm"] = "Test#ARC14",
       ["AlgorandAuthentication:CheckExpiration"] = "false",
       ["AlgorandAuthentication:AllowedNetworks:0:Name"] = "mainnet",
       ["AlgorandAuthentication:AllowedNetworks:0:AlgodApiUrl"] = "https://mainnet-api.4160.nodely.dev",
       ["IPFSConfig:ApiUrl"] = "https://ipfs.infura.io:5001",
       ["IPFSConfig:GatewayUrl"] = "https://ipfs.io/ipfs/",
       ["EVMChains:0:ChainId"] = "8453",
       ["EVMChains:0:Name"] = "Base",
       ["EVMChains:0:RpcUrl"] = "https://mainnet.base.org",
       ["Debug:EmptySuccessOnFailure"] = "false",
       ["CorsSettings:AllowedOrigins:0"] = "*"
   };
   ```

#### Common Issues and Solutions

**Issue**: Tests pass locally but fail in CI with timing errors
**Solution**: Increase retry count and delays. CI needs 2-4x more time than local.

**Issue**: Tests fail with "Address already in use" errors
**Solution**: Add `[NonParallelizable]` to test class.

**Issue**: Tests fail with "Required service not configured" errors
**Solution**: Add ALL config sections to WebApplicationFactory configuration dictionary.

**Issue**: Tests intermittently timeout
**Solution**: Implement retry logic with exponential backoff for health endpoint calls.

#### Verification Checklist for New Integration Tests

- [ ] Test class has `[NonParallelizable]` attribute
- [ ] Configuration includes ALL required sections (use template above)
- [ ] Health checks use retry logic (10+ retries, 2s+ delays)
- [ ] Tests pass locally 10+ times consecutively
- [ ] Tests pass in CI (wait for CI run before merging)
- [ ] No port conflicts with other test classes

**Lesson Learned (2026-02-10)**: Even with all mitigations (NonParallelizable, complete config, retry logic), CI resource constraints can cause intermittent failures. Always err on the side of MORE retries and LONGER delays for CI environments. Local success does NOT guarantee CI success.

## Questions and Clarifications

If you encounter ambiguous requirements or need to make architectural decisions:
1. Check existing patterns in the codebase first
2. Follow .NET and C# best practices
3. Maintain consistency with existing code style
4. Prioritize security and data integrity
5. Ask the user if uncertain about blockchain-specific requirements

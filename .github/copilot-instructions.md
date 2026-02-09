# GitHub Copilot Instructions for BiatecTokensApi

## Project Overview

BiatecTokensApi is a comprehensive .NET 8.0 Web API for deploying and managing various types of tokens on different blockchain networks, including ERC20 tokens on EVM chains (Base blockchain) and multiple Algorand token standards (ASA, ARC3, ARC200).

## Technology Stack

- **Framework**: .NET 8.0 (C#)
- **IDE**: Visual Studio 2022 or Visual Studio Code
- **Package Manager**: NuGet
- **Testing Framework**: xUnit
- **API Documentation**: Swagger/OpenAPI (Swashbuckle)
- **Blockchain Libraries**:
  - Algorand4 (v4.0.3.2025051817) - Algorand blockchain integration
  - Nethereum.Web3 (v5.0.0) - Ethereum/EVM blockchain integration
  - AlgorandAuthentication (v2.0.1) - ARC-0014 authentication
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

## Dependency Updates and Dependabot PRs

### Handling Dependabot Pull Requests

Dependabot PRs often show **false positive CI failures** due to GitHub Actions permission restrictions. Follow this workflow to properly evaluate them:

#### 1. Identify Dependabot Permission Errors
Look for these indicators of a **false positive failure**:
- Error message: `RequestError [HttpError]: Resource not accessible by integration`
- HTTP Status: `403 Forbidden`
- URL pattern: `https://api.github.com/repos/.../issues/.../comments`
- Error location: **AFTER** test execution completes
- Message content: Workflow trying to post "✅ CI checks passed!" comment

**This is NOT a test or build failure** - it's a GitHub security restriction on Dependabot PRs.

#### 2. Verify Tests Locally
**ALWAYS** verify dependency updates locally before assuming CI failure:

```bash
# Standard dependency update verification workflow
cd /path/to/BiatecTokensApi

# Step 1: Restore dependencies
dotnet restore
# Expected: Success (warnings about version constraints are usually safe)

# Step 2: Build in Release mode
dotnet build --configuration Release --no-restore
# Expected: 0 Errors, ~97 warnings (pre-existing)

# Step 3: Run tests (exclude RealEndpoint tests)
dotnet test --configuration Release --no-build --verbosity normal \
  --filter "FullyQualifiedName!~RealEndpoint"
# Expected baseline: ~1397 tests passing out of 1401 total
# Pass rate should be ≥99.5%
```

#### 3. Evaluate Dependency Security Impact

For each updated dependency, assess:

**Critical Security Updates (P0 - Fast Track)**:
- `System.IdentityModel.Tokens.Jwt`: JWT validation, log sanitization, crypto algorithms
- `Microsoft.AspNetCore.Authentication.JwtBearer`: Authentication security
- `Nethereum.Web3`: Smart contract security, EVM interactions
- `NBitcoin`: Cryptographic operations, BIP39 mnemonics

**API Stability Updates (P1 - Important)**:
- `Microsoft.OpenApi`: OpenAPI spec compatibility
- `Swashbuckle.AspNetCore.*`: Swagger UI, API documentation
- `Algorand4`: Algorand protocol updates
- `AlgorandAuthentication`: ARC-14 auth security

**Testing Infrastructure (P2 - Standard)**:
- `Microsoft.NET.Test.Sdk`: Test framework updates
- `coverlet.*`: Code coverage tools
- `NUnit.*`: Test framework and adapters
- `Moq`: Mocking library updates

#### 4. Document Business Value

Every dependency PR must document:

**Test Coverage**:
- Which tests validate the updated dependencies
- Pass rate (should match ~1397/1401 baseline)
- Any new test failures or regressions

**Security Impact**:
- CVE fixes (link to security advisories)
- Log sanitization improvements
- Cryptographic algorithm updates
- Authentication/authorization changes

**Business Context**:
- Link to product roadmap requirement
- Compliance impact (GDPR, MICA, etc.)
- Customer-facing improvements
- Risk if update is delayed

#### 5. Workflow Fixes for Dependabot PRs

GitHub Actions steps that post comments or create checks **must** include:

```yaml
- name: Comment PR with results
  if: github.actor != 'dependabot[bot]'  # Skip for Dependabot
  continue-on-error: true                 # Don't fail on permission errors
  uses: actions/github-script@v8
  with:
    script: |
      # Your comment logic here
```

**Required conditions**:
- `if: github.actor != 'dependabot[bot]'`: Skip Dependabot PRs entirely
- `continue-on-error: true`: Don't fail workflow on permission errors

#### 6. Common Dependabot False Positive Patterns

| Symptom | Root Cause | Resolution |
|---------|-----------|------------|
| "Resource not accessible by integration" | GitHub token has read-only permissions | Skip comment step for Dependabot |
| Workflow "failed" but tests show "Passed" | Permission error after successful tests | Verify tests locally, merge if passing |
| 403 Forbidden on PR comment | Dependabot security restrictions | Add `continue-on-error: true` to step |
| Unable to publish test results | No write permission to create checks | Use `continue-on-error: true` |

#### 7. Quality Standards for Dependency PRs

**Before Merging**, confirm:
- ✅ All tests pass (1397/1401 baseline)
- ✅ Build succeeds (0 errors)
- ✅ No new security vulnerabilities introduced
- ✅ Documentation updated (if API surface changes)
- ✅ Business value documented (security fixes, compliance, etc.)
- ✅ Breaking changes assessed (should be none for patch/minor updates)

**After Merging**, monitor:
- Application startup (no new exceptions)
- Log output (verify log sanitization working)
- API endpoints (no regression in response format)
- Test pass rate (should remain at baseline)

#### 8. Dependency Update Prioritization

| Priority | Type | SLA | Examples |
|----------|------|-----|----------|
| P0 | Security Critical | 24 hours | JWT vulnerabilities, crypto bugs |
| P1 | API Breaking | 1 week | OpenAPI incompatibility, auth changes |
| P2 | Standard Updates | 2 weeks | Minor version bumps, tooling updates |
| P3 | Nice-to-Have | 1 month | Documentation improvements, dev tools |

#### 9. When to Reject a Dependabot PR

Reject or defer if:
- ❌ Tests fail locally (actual regression)
- ❌ Breaking changes without migration path
- ❌ Major version jump without evaluation
- ❌ Dependency introduces new vulnerabilities
- ❌ Conflicts with other critical changes in flight

#### 10. Lessons Learned

**Problem**: Dependabot PRs consistently show "failed" status in GitHub UI, causing confusion and blocking merges.

**Root Cause**: GitHub restricts Dependabot PR tokens to read-only for security. Workflows that post comments or create checks fail with 403 errors **after** tests pass.

**Solution**: 
1. Always verify tests locally: `dotnet test --configuration Release --no-build --filter "FullyQualifiedName!~RealEndpoint"`
2. Add `continue-on-error: true` to all PR comment/check steps
3. Skip comment steps for Dependabot: `if: github.actor != 'dependabot[bot]'`
4. Document test results and security impact in PR description
5. Trust local verification over CI status when CI fails on permissions

**Prevention**: Updated `.github/workflows/test-pr.yml` to handle Dependabot gracefully. Future dependency PRs should show accurate status.

## Support and Resources

- API Documentation: Available at `/swagger` endpoint
- Repository: https://github.com/scholtz/BiatecTokensApi
- Algorand Documentation: https://developer.algorand.org
- Nethereum Documentation: https://docs.nethereum.com
- ARC Standards: https://github.com/algorandfoundation/ARCs

## Questions and Clarifications

If you encounter ambiguous requirements or need to make architectural decisions:
1. Check existing patterns in the codebase first
2. Follow .NET and C# best practices
3. Maintain consistency with existing code style
4. Prioritize security and data integrity
5. Ask the user if uncertain about blockchain-specific requirements

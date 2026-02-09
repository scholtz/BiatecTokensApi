# GitHub Copilot Instructions for BiatecTokensApi

## Project Overview

BiatecTokensApi is a comprehensive .NET 8.0 Web API for deploying and managing various types of tokens on different blockchain networks, including ERC20 tokens on EVM chains (Base blockchain) and multiple Algorand token standards (ASA, ARC3, ARC200).

## Technology Stack

- **Framework**: .NET 10.0 (C#)
- **IDE**: Visual Studio 2022 or Visual Studio Code
- **Package Manager**: NuGet
- **Testing Framework**: NUnit
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

## Questions and Clarifications

If you encounter ambiguous requirements or need to make architectural decisions:
1. Check existing patterns in the codebase first
2. Follow .NET and C# best practices
3. Maintain consistency with existing code style
4. Prioritize security and data integrity
5. Ask the user if uncertain about blockchain-specific requirements

## Dependency Updates and Dependabot PRs

### Understanding Dependabot CI "Failures"

**CRITICAL: Dependabot PRs often show "failed" CI status, but this is NOT an actual test or build failure.**

#### Root Cause
- Dependabot PRs run with **read-only GitHub token permissions** by default
- Workflow steps that post comments or create checks will fail with **HTTP 403 "Resource not accessible by integration"**
- This is a **GitHub Actions permission limitation**, not a code problem
- The actual tests and build typically **pass successfully** before the permission error occurs

#### How to Verify Dependency Updates

When investigating a "failed" Dependabot PR CI run:

1. **Check the workflow logs carefully**:
   - Look for "Publishing success results" or similar success messages
   - Identify where the failure actually occurs (usually at comment/publish steps)
   - Verify tests passed before the 403 error

2. **Verify locally** (this is the source of truth):
   ```bash
   # Standard verification workflow
   dotnet restore
   dotnet build --configuration Release --no-restore
   dotnet test --configuration Release --no-build --filter "FullyQualifiedName!~RealEndpoint"
   ```

3. **Check test baseline**:
   - Expected: ~1397/1401 tests passing (99.7%)
   - If local tests pass, the dependency update is safe

4. **Review dependency changes**:
   - Check release notes for security fixes (e.g., log sanitization, CVE fixes)
   - Verify compatibility with current .NET version
   - Look for breaking changes or deprecations
   - Document security impact and business value

### Workflow Best Practices for Dependabot

#### Required Permissions
All workflows that comment on PRs or create checks MUST include explicit permissions:

```yaml
permissions:
  contents: read
  pull-requests: write
  checks: write
  issues: write
```

#### Handling Dependabot PRs in Workflows

For steps that comment on PRs or publish results:

```yaml
- name: Publish test results
  uses: EnricoMi/publish-unit-test-result-action@v2
  if: always() && github.actor != 'dependabot[bot]'
  continue-on-error: true  # Prevent workflow failure if step is skipped
  with:
    files: '**/test-results.trx'

- name: Comment PR with results
  if: github.event_name == 'pull_request' && always() && github.actor != 'dependabot[bot]'
  continue-on-error: true  # Gracefully handle permission errors
  uses: actions/github-script@v8
  with:
    script: |
      # Comment logic here
```

**Key Pattern Elements**:
1. `github.actor != 'dependabot[bot]'` - Skip steps for Dependabot
2. `continue-on-error: true` - Don't fail workflow if step fails
3. Explicit permissions at workflow level
4. Keep test execution steps without Dependabot conditions (tests should always run)

#### Common Mistakes to Avoid

❌ **DON'T**:
- Assume CI failure means tests failed
- Skip local verification for "failed" Dependabot PRs
- Remove the actual test execution steps
- Add Dependabot conditions to build/test steps
- Merge without verifying tests pass locally

✅ **DO**:
- Always verify tests locally for Dependabot PRs
- Check workflow logs to identify the actual failure point
- Add Dependabot conditions only to comment/publish steps
- Document security impact of dependency updates
- Verify compatibility and breaking changes

### Dependency Update Verification Checklist

When reviewing Dependabot PRs:

- [ ] Verify tests pass locally (run full test suite)
- [ ] Check workflow logs for actual failure location
- [ ] Review dependency release notes for:
  - [ ] Security fixes (CVEs, vulnerabilities)
  - [ ] Breaking changes
  - [ ] Deprecations
  - [ ] New features
- [ ] Document business value:
  - [ ] Security posture improvement
  - [ ] Compliance requirements
  - [ ] Bug fixes
  - [ ] Performance improvements
- [ ] Check compatibility:
  - [ ] .NET version compatibility
  - [ ] Other dependency conflicts
  - [ ] API surface changes
- [ ] Update documentation if API changes
- [ ] Verify no regression in test coverage

### Example Dependency Update PR Review

```markdown
## Dependency Update Verification

### Changes
- System.IdentityModel.Tokens.Jwt: 8.3.1 → 8.15.0
- Swashbuckle.AspNetCore: 10.1.1 → 10.1.2

### Security Impact
✅ **CRITICAL**: System.IdentityModel.Tokens.Jwt 8.15.0 includes log sanitization (PR #3316)
   - Prevents sensitive data leaks in logs
   - Addresses potential security vulnerability

✅ Swashbuckle.AspNetCore 10.1.2 fixes browser caching and URL serialization issues

### Verification Results
✅ Local tests: 1397/1401 passing (99.7%)
✅ Build: 0 errors, 97 warnings (existing)
✅ No breaking changes detected
✅ All API endpoints functional

### Business Value
- **Security**: P0 - Prevents log injection attacks
- **Compliance**: Required for production deployment
- **Risk**: Low - No breaking changes

### Recommendation
✅ **APPROVED** - Safe to merge after CI workflow fix
```

### When to Update Copilot Instructions

Update these instructions if:
1. GitHub Actions permission model changes
2. New patterns emerge for handling Dependabot PRs
3. Test baseline thresholds change
4. New security requirements are added
5. Dependency verification process changes

---

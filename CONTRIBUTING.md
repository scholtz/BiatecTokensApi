# Contributing to BiatecTokensApi

Thank you for your interest in contributing to BiatecTokensApi! This document provides guidelines for contributing to the project.

## Table of Contents

- [Getting Started](#getting-started)
- [Development Setup](#development-setup)
- [Testing](#testing)
  - [Running Tests](#running-tests)
  - [Test Structure](#test-structure)
  - [Writing Tests](#writing-tests)
  - [Test Categories](#test-categories)
- [CI/CD Requirements](#cicd-requirements)
- [Code Style](#code-style)
- [Pull Request Process](#pull-request-process)

## Getting Started

Before contributing, please:

1. Fork the repository
2. Create a feature branch from `master`
3. Make your changes following our guidelines
4. Ensure all tests pass
5. Submit a pull request

## Development Setup

### Prerequisites

- .NET 8.0 SDK
- Visual Studio 2022 or Visual Studio Code
- Git

### Clone and Build

```bash
# Clone the repository
git clone https://github.com/scholtz/BiatecTokensApi.git
cd BiatecTokensApi

# Restore dependencies
dotnet restore BiatecTokensApi.sln

# Build the solution
dotnet build BiatecTokensApi.sln --configuration Release

# Run the API locally
dotnet run --project BiatecTokensApi/BiatecTokensApi.csproj
```

The API will be available at `https://localhost:7000` with Swagger documentation at `https://localhost:7000/swagger`.

### Configuration

The API requires configuration for:
- **Algorand networks** - configured in `AlgorandAuthentication.AllowedNetworks`
- **EVM chains** - configured in `EVMChains` array
- **IPFS** - configured in `IPFSConfig`
- **Account mnemonic** - stored in User Secrets or environment variables

**Important**: Never commit secrets or mnemonics to source control. Use User Secrets for local development:

```bash
dotnet user-secrets set "App:Account" "your-mnemonic-phrase" --project BiatecTokensApi/BiatecTokensApi.csproj
dotnet user-secrets set "IPFSConfig:Username" "your-ipfs-username" --project BiatecTokensApi/BiatecTokensApi.csproj
dotnet user-secrets set "IPFSConfig:Password" "your-ipfs-password" --project BiatecTokensApi/BiatecTokensApi.csproj
```

## Testing

### Running Tests

#### Run All Tests (Excluding External Dependencies)

```bash
# From the repository root
dotnet test BiatecTokensTests --filter "FullyQualifiedName!~RealEndpoint"
```

This command runs all tests except those that require real external endpoints (IPFS, blockchain nodes).

#### Run Tests with Code Coverage

```bash
dotnet test BiatecTokensTests \
  --filter "FullyQualifiedName!~RealEndpoint" \
  --collect:"XPlat Code Coverage"
```

#### Run Specific Test Class

```bash
# Run only TokenControllerTests
dotnet test BiatecTokensTests --filter "FullyQualifiedName~TokenControllerTests"

# Run only TokenServiceTests  
dotnet test BiatecTokensTests --filter "FullyQualifiedName~TokenServiceTests"

# Run only IPFSRepositoryTests
dotnet test BiatecTokensTests --filter "FullyQualifiedName~IPFSRepositoryTests"
```

#### Run a Specific Test Method

```bash
dotnet test BiatecTokensTests --filter "FullyQualifiedName~TokenControllerTests.ERC20MintableTokenCreate_WithValidRequest_ReturnsOkResult"
```

#### Run Tests with Detailed Output

```bash
dotnet test BiatecTokensTests \
  --filter "FullyQualifiedName!~RealEndpoint" \
  --verbosity detailed
```

### Test Structure

The `BiatecTokensTests` project follows a clear organization:

```
BiatecTokensTests/
├── TokenControllerTests.cs       # API Controller tests
├── TokenServiceTests.cs          # ERC20 Token Service tests
├── IPFSRepositoryTests.cs        # IPFS Repository unit tests (mocked)
├── IPFSRepositoryIntegrationTests.cs  # IPFS integration tests
├── IPFSRepositoryRealEndpointTests.cs # IPFS tests with real endpoints
├── ApiIntegrationTests.cs        # Full API integration tests
├── Erc20TokenTests.cs            # ERC20 token tests with local blockchain
├── TDDExampleTests.cs            # TDD examples and patterns
├── TestHelper.cs                 # Shared test utilities
└── README.md                     # Test-specific documentation
```

### Writing Tests

#### Test Framework

We use **NUnit 3** as the testing framework with **Moq** for mocking dependencies.

#### Test Naming Convention

Follow the pattern: `MethodName_Scenario_ExpectedResult`

```csharp
[Test]
public async Task CreateToken_WithValidRequest_ReturnsSuccess()
{
    // Test implementation
}

[Test]
public async Task CreateToken_WithInvalidRequest_ReturnsBadRequest()
{
    // Test implementation
}
```

#### Test Structure (AAA Pattern)

Organize tests using the Arrange-Act-Assert pattern:

```csharp
[Test]
public async Task ERC20MintableTokenCreate_WithValidRequest_ReturnsOkResult()
{
    // Arrange - Set up test data and mocks
    var expectedResponse = new ERC20TokenDeploymentResponse
    {
        Success = true,
        ContractAddress = "0x1234...",
        TransactionHash = "0xabcd..."
    };
    
    _tokenServiceMock
        .Setup(x => x.DeployERC20TokenAsync(It.IsAny<Request>(), TokenType.ERC20_Mintable))
        .ReturnsAsync(expectedResponse);

    // Act - Execute the method being tested
    var result = await _controller.ERC20MintableTokenCreate(request);

    // Assert - Verify the results
    Assert.That(result, Is.InstanceOf<OkObjectResult>());
    var okResult = result as OkObjectResult;
    Assert.That(okResult!.Value, Is.EqualTo(expectedResponse));
}
```

#### Using Mocks

Use Moq to create mock dependencies:

```csharp
private Mock<IERC20TokenService> _tokenServiceMock;
private Mock<ILogger<TokenController>> _loggerMock;

[SetUp]
public void Setup()
{
    _tokenServiceMock = new Mock<IERC20TokenService>();
    _loggerMock = new Mock<ILogger<TokenController>>();
    
    _controller = new TokenController(
        _tokenServiceMock.Object,
        _loggerMock.Object
    );
}
```

#### Assertions

Use NUnit's constraint model for assertions:

```csharp
// Preferred
Assert.That(actual, Is.EqualTo(expected));
Assert.That(result, Is.Not.Null);
Assert.That(list, Has.Count.EqualTo(3));

// Avoid classic model
Assert.AreEqual(expected, actual); // Don't use
Assert.IsNotNull(result); // Don't use
```

### Test Categories

#### Unit Tests

- Test individual components in isolation
- Use mocks for all dependencies
- Fast execution (< 1 second per test)
- Examples: `TokenControllerTests`, `TokenServiceTests`

#### Integration Tests

- Test multiple components together
- May use test databases or in-memory implementations
- Moderate execution time
- Examples: `IPFSRepositoryIntegrationTests`, `ApiIntegrationTests`

#### Tests Requiring External Services

Tests marked with `[Category("RealEndpoint")]` or containing "RealEndpoint" in their name:
- Require live IPFS endpoints
- Require local blockchain nodes (Ganache)
- **Excluded from CI** pipeline
- Run manually for verification

**Examples**:
- `IPFSRepositoryRealEndpointTests` - Tests with real IPFS endpoints
- `Erc20TokenTests` - Requires local Ethereum node at `http://127.0.0.1:8545`

To run tests with external dependencies:

```bash
# Run IPFS real endpoint tests
dotnet test BiatecTokensTests --filter "FullyQualifiedName~RealEndpoint"

# Run ERC20 tests (requires Ganache)
dotnet test BiatecTokensTests --filter "FullyQualifiedName~Erc20TokenTests"
```

## CI/CD Requirements

### Continuous Integration

Our CI pipeline (`.github/workflows/test-pr.yml`) runs on every pull request and includes:

1. **Build Check**
   ```bash
   dotnet restore BiatecTokensApi.sln
   dotnet build BiatecTokensApi.sln --configuration Release --no-restore
   ```

2. **Unit Tests**
   ```bash
   dotnet test BiatecTokensTests/BiatecTokensTests.csproj \
     --configuration Release \
     --no-build \
     --verbosity normal \
     --logger "trx;LogFileName=test-results.trx" \
     --collect:"XPlat Code Coverage" \
     --filter "FullyQualifiedName!~RealEndpoint"
   ```

3. **OpenAPI Specification Generation**
   - Generates OpenAPI spec using Swashbuckle
   - Uploads as workflow artifact
   - Available at runtime via `/swagger/v1/swagger.json`

### Required Checks

Before merging a PR, ensure:

- ✅ All unit tests pass
- ✅ Code builds without errors
- ✅ No new compiler warnings introduced
- ✅ Code follows existing patterns and conventions
- ✅ New features include appropriate tests
- ✅ Documentation is updated if needed

### Test Coverage Goals

While we don't have strict coverage requirements, aim for:
- **Critical paths**: 80%+ coverage (token deployment, authentication)
- **Business logic**: 70%+ coverage (service methods)
- **Controllers**: Basic happy path and error cases covered

## Code Style

### General Guidelines

- Follow standard C# naming conventions (PascalCase for public members, camelCase for private fields)
- Use explicit types instead of `var` when type is not obvious
- Enable nullable reference types
- Add XML documentation comments for public APIs
- Keep methods focused and single-responsibility

### Documentation

Add XML documentation for public APIs:

```csharp
/// <summary>
/// Creates a new ERC20 mintable token on the specified EVM chain.
/// </summary>
/// <param name="request">The token creation request containing token parameters.</param>
/// <returns>The transaction result including contract address and transaction hash.</returns>
/// <exception cref="ArgumentNullException">Thrown when request is null.</exception>
public async Task<ERC20TokenDeploymentResponse> CreateERC20MintableAsync(CreateRequest request)
{
    // Implementation
}
```

### Security Best Practices

- **Never** commit secrets, private keys, or mnemonics
- Use User Secrets for local development
- Use environment variables for production
- Validate all input parameters
- Handle errors without leaking sensitive information
- Always test on testnet before mainnet

## Pull Request Process

1. **Create a Feature Branch**
   ```bash
   git checkout -b feature/your-feature-name
   ```

2. **Make Your Changes**
   - Write code following our conventions
   - Add/update tests for your changes
   - Update documentation as needed

3. **Test Your Changes**
   ```bash
   # Build
   dotnet build BiatecTokensApi.sln
   
   # Run tests
   dotnet test BiatecTokensTests --filter "FullyQualifiedName!~RealEndpoint"
   ```

4. **Commit Your Changes**
   ```bash
   git add .
   git commit -m "feat: Add feature description"
   ```
   
   Use conventional commit messages:
   - `feat:` for new features
   - `fix:` for bug fixes
   - `docs:` for documentation changes
   - `test:` for test additions/changes
   - `refactor:` for code refactoring

5. **Push and Create PR**
   ```bash
   git push origin feature/your-feature-name
   ```
   
   Create a pull request on GitHub with:
   - Clear title and description
   - Reference to related issues
   - Screenshots for UI changes
   - Test results summary

6. **Review Process**
   - CI checks must pass
   - Code review by maintainers
   - Address review feedback
   - Squash commits if requested

7. **Merge**
   - PR will be merged by maintainers
   - Delete your feature branch after merge

## Getting Help

- **Issues**: Report bugs or request features via [GitHub Issues](https://github.com/scholtz/BiatecTokensApi/issues)
- **Discussions**: Ask questions in [GitHub Discussions](https://github.com/scholtz/BiatecTokensApi/discussions)
- **Documentation**: Check the main [README.md](./README.md) and [API documentation](https://your-api-url/swagger)

## License

By contributing to BiatecTokensApi, you agree that your contributions will be licensed under the same license as the project.

---

Thank you for contributing to BiatecTokensApi! 🚀

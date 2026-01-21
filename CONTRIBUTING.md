# Contributing to BiatecTokensApi

Thank you for your interest in contributing to BiatecTokensApi! This document provides guidelines and instructions for contributing to the project.

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Getting Started](#getting-started)
- [Development Setup](#development-setup)
- [Testing](#testing)
- [Submitting Changes](#submitting-changes)
- [Code Style](#code-style)

## Code of Conduct

By participating in this project, you agree to maintain a respectful and inclusive environment for all contributors.

## Getting Started

1. Fork the repository on GitHub
2. Clone your fork locally
3. Create a new branch for your feature or bug fix
4. Make your changes
5. Submit a pull request

## Development Setup

### Prerequisites

- .NET 8.0 SDK or later
- Visual Studio 2022 or Visual Studio Code
- Docker (optional, for containerized deployment)

### Building the Project

```bash
# Restore dependencies
dotnet restore BiatecTokensApi.sln

# Build the solution
dotnet build BiatecTokensApi.sln --configuration Release

# Build in Debug mode with XML documentation
dotnet build BiatecTokensApi.sln --configuration Debug
```

### Running the API Locally

```bash
# Run the API
dotnet run --project BiatecTokensApi/BiatecTokensApi.csproj

# The API will be available at https://localhost:7000
# Swagger documentation: https://localhost:7000/swagger
```

### Configuration

The API requires configuration for:
- Algorand networks and authentication (ARC-0014)
- IPFS integration for ARC3 metadata
- EVM blockchain settings for ERC20 tokens

For local development, use user secrets:

```bash
dotnet user-secrets set "App:Account" "your-mnemonic-phrase"
dotnet user-secrets set "IPFSConfig:Username" "your-ipfs-username"
dotnet user-secrets set "IPFSConfig:Password" "your-ipfs-password"
```

**⚠️ NEVER commit secrets, private keys, or mnemonics to the repository.**

## Testing

### Running Tests Locally

The project uses NUnit for unit and integration testing. We maintain high test coverage standards to ensure code quality and reliability.

#### Run All Tests

```bash
# Run all tests
dotnet test BiatecTokensTests/BiatecTokensTests.csproj --verbosity normal

# Run tests excluding integration tests that require real endpoints
dotnet test BiatecTokensTests/BiatecTokensTests.csproj \
  --filter "FullyQualifiedName!~RealEndpoint" \
  --verbosity normal
```

#### Run Tests with Coverage

```bash
# Run tests with code coverage collection
dotnet test BiatecTokensTests/BiatecTokensTests.csproj \
  --collect:"XPlat Code Coverage" \
  --filter "FullyQualifiedName!~RealEndpoint" \
  --verbosity normal

# Generate HTML coverage report (requires reportgenerator tool)
dotnet tool install --global dotnet-reportgenerator-globaltool
reportgenerator \
  -reports:"**/coverage.cobertura.xml" \
  -targetdir:"coveragereport" \
  -reporttypes:Html
```

#### Run Specific Test Classes

```bash
# Run specific test class
dotnet test BiatecTokensTests/BiatecTokensTests.csproj \
  --filter "FullyQualifiedName~TokenServiceTests" \
  --verbosity detailed

# Run specific test method
dotnet test BiatecTokensTests/BiatecTokensTests.csproj \
  --filter "FullyQualifiedName~TokenServiceTests.SpecificTestMethod" \
  --verbosity detailed
```

### Coverage Requirements

The project enforces code coverage thresholds in CI to maintain and improve code quality. We are on an incremental path to reach target coverage levels.

**Current Thresholds** (enforced in CI):
- **Line Coverage**: Minimum 15%
- **Branch Coverage**: Minimum 8%

**Target Coverage** (to be reached incrementally):
- **Line Coverage**: 80%
- **Branch Coverage**: 70%

Pull requests that reduce coverage below the current thresholds will fail CI checks. We encourage contributors to add comprehensive tests with each PR to help us reach the target coverage levels.

**Coverage Progress**:
- ✅ Initial: 11.66% line / 3.32% branch
- ✅ Phase 1: 15% line / 8% branch (Current - validation & models)
- 🎯 Phase 2: 35% line / 25% branch (Next milestone - service layer with mocks)
- 🎯 Phase 3: 60% line / 50% branch (Integration tests)
- 🎯 Phase 4: 80% line / 70% branch (Target - comprehensive coverage)

### Writing Tests

#### Test Structure

Follow the AAA (Arrange-Act-Assert) pattern:

```csharp
[Test]
public void MethodName_Scenario_ExpectedResult()
{
    // Arrange - Set up test data and dependencies
    var mockService = new Mock<IService>();
    var sut = new SystemUnderTest(mockService.Object);
    
    // Act - Execute the method being tested
    var result = sut.MethodName(input);
    
    // Assert - Verify the expected outcome
    Assert.That(result, Is.Not.Null);
    Assert.That(result.Success, Is.True);
}
```

#### Test Naming Convention

Use descriptive test names that clearly indicate:
- The method being tested
- The scenario or condition
- The expected outcome

Examples:
- `CreateToken_ValidRequest_ReturnsSuccess`
- `CreateToken_NullRequest_ThrowsArgumentNullException`
- `CreateToken_InvalidChainId_ReturnsError`

#### Mocking Dependencies

Use Moq for mocking dependencies:

```csharp
var mockLogger = new Mock<ILogger<MyService>>();
var mockConfig = new Mock<IOptionsMonitor<MyConfig>>();
mockConfig.Setup(x => x.CurrentValue).Returns(new MyConfig { /* setup */ });
```

#### Test Categories

- **Unit Tests**: Test individual methods and classes in isolation with mocked dependencies
- **Integration Tests**: Test interactions between components
- **Real Endpoint Tests**: Tests that require actual network connections (excluded from CI by default)

### Test Files

The test project is organized as follows:

```
BiatecTokensTests/
├── ApiIntegrationTests.cs          # End-to-end API tests
├── Erc20TokenTests.cs              # ERC20 token functionality tests
├── TokenServiceTests.cs            # Token service unit tests
├── TokenControllerTests.cs         # Controller endpoint tests
├── IPFSRepositoryTests.cs          # IPFS integration tests
├── IPFSRepositoryIntegrationTests.cs  # IPFS integration with mocks
├── IPFSRepositoryRealEndpointTests.cs  # Real IPFS endpoint tests (excluded from CI)
├── TDDExampleTests.cs              # TDD examples and patterns
└── TestHelper.cs                   # Shared test utilities
```

### Debugging Tests

In Visual Studio:
1. Open Test Explorer (Test > Test Explorer)
2. Right-click a test and select "Debug"

In Visual Studio Code:
1. Install the ".NET Core Test Explorer" extension
2. Use the Test Explorer sidebar to run/debug tests

From command line:
```bash
# Run tests in debug mode with detailed output
dotnet test BiatecTokensTests/BiatecTokensTests.csproj \
  --verbosity detailed \
  --logger "console;verbosity=detailed"
```

## Submitting Changes

### Pull Request Process

1. **Create a feature branch** from `master`
   ```bash
   git checkout -b feature/your-feature-name
   ```

2. **Make your changes** following the code style guidelines

3. **Add tests** for new functionality
   - Ensure all new code has adequate test coverage
   - Run tests locally before submitting

4. **Update documentation** if needed
   - Update XML documentation comments
   - Update README.md if adding new features
   - Update OPENAPI.md for API changes

5. **Run all checks locally**
   ```bash
   # Build
   dotnet build BiatecTokensApi.sln --configuration Release
   
   # Run tests with coverage
   dotnet test BiatecTokensTests/BiatecTokensTests.csproj \
     --collect:"XPlat Code Coverage" \
     --filter "FullyQualifiedName!~RealEndpoint"
   ```

6. **Commit your changes** with clear, descriptive commit messages
   ```bash
   git add .
   git commit -m "Add feature: description of change"
   ```

7. **Push to your fork**
   ```bash
   git push origin feature/your-feature-name
   ```

8. **Open a Pull Request** on GitHub
   - Provide a clear title and description
   - Reference any related issues
   - Tag reviewers if needed

### Pull Request Requirements

Before your pull request can be merged, it must:

- ✅ Pass all CI checks (build, tests, coverage thresholds)
- ✅ Meet code coverage thresholds (current: 15% line, 8% branch; target: 80% line, 70% branch)
- ✅ Receive at least **1 approval** from a maintainer or code owner
- ✅ Have all conversations resolved
- ✅ Have no merge conflicts with `master`
- ✅ Include tests for new functionality
- ✅ Follow the project's code style
- ✅ Have clear, descriptive commit messages
- ✅ Include updated documentation if needed

**Note**: Branch protection rules should be configured by repository administrators to enforce these requirements. See [BRANCH_PROTECTION.md](BRANCH_PROTECTION.md) for configuration details.

## Code Style

### C# Coding Conventions

Follow standard C# naming conventions:
- **PascalCase** for public members, classes, methods, properties
- **camelCase** for private fields, local variables, parameters
- Prefix private fields with underscore: `_privateField`

### Documentation

- Add XML documentation comments (`///`) for all public APIs
- Include `<summary>`, `<param>`, `<returns>`, and `<exception>` tags
- Documentation is generated in Debug builds to `doc/documentation.xml`

Example:
```csharp
/// <summary>
/// Creates a new ERC20 token with the specified parameters.
/// </summary>
/// <param name="request">The token creation request.</param>
/// <returns>The deployment response with transaction details.</returns>
/// <exception cref="ArgumentNullException">Thrown when request is null.</exception>
public async Task<TokenCreationResponse> CreateTokenAsync(CreateTokenRequest request)
{
    // Implementation
}
```

### General Guidelines

- Use explicit types instead of `var` when type is not obvious
- Enable and address nullable reference types warnings
- Keep methods focused and single-purpose
- Prefer composition over inheritance
- Use dependency injection for services
- Validate all input parameters
- Handle exceptions appropriately
- Log important operations and errors
- Never expose sensitive information in logs or errors

### Security Best Practices

- ⚠️ Never commit secrets, API keys, or private keys
- Use user secrets for local development
- Use environment variables for production
- Validate and sanitize all inputs
- Use proper authentication and authorization
- Follow blockchain security best practices
- Test on testnet before mainnet

## Getting Help

If you have questions or need help:

1. Check existing issues on GitHub
2. Review the documentation in README.md and OPENAPI.md
3. Ask questions in pull request discussions
4. Review the custom instructions in `.github/copilot-instructions.md`

## License

By contributing to this project, you agree that your contributions will be licensed under the same license as the project.

Thank you for contributing to BiatecTokensApi! 🚀

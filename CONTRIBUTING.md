# Contributing to BiatecTokensApi

Welcome! We're excited that you're interested in contributing to BiatecTokensApi. This document provides guidelines and instructions for contributing to this project.

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Getting Started](#getting-started)
- [Development Setup](#development-setup)
- [Testing Guidelines](#testing-guidelines)
- [Code Coverage Requirements](#code-coverage-requirements)
- [Pull Request Process](#pull-request-process)
- [Coding Standards](#coding-standards)
- [Branch Protection Rules](#branch-protection-rules)

## Code of Conduct

By participating in this project, you agree to maintain a respectful and collaborative environment. Please be kind, constructive, and professional in all interactions.

## Getting Started

1. Fork the repository
2. Clone your fork: `git clone https://github.com/YOUR-USERNAME/BiatecTokensApi.git`
3. Create a feature branch: `git checkout -b feature/your-feature-name`
4. Make your changes following our guidelines
5. Push to your fork and submit a pull request

## Development Setup

### Prerequisites

- .NET 8.0 SDK or later
- Visual Studio 2022, VS Code, or JetBrains Rider
- Docker (optional, for containerized deployment)

### Initial Setup

```bash
# Clone the repository
git clone https://github.com/scholtz/BiatecTokensApi.git
cd BiatecTokensApi

# Restore dependencies
dotnet restore BiatecTokensApi.sln

# Build the solution
dotnet build BiatecTokensApi.sln --configuration Release

# Set up user secrets for local development (recommended)
cd BiatecTokensApi
dotnet user-secrets set "App:Account" "your-test-mnemonic-for-development"
dotnet user-secrets set "IPFSConfig:Username" "your-ipfs-username"
dotnet user-secrets set "IPFSConfig:Password" "your-ipfs-password"
cd ..
```

## Testing Guidelines

### Our Testing Philosophy

We follow **Test-Driven Development (TDD)** principles:

1. **Red**: Write a failing test that describes the desired behavior
2. **Green**: Implement minimal code to make the test pass
3. **Refactor**: Improve code while keeping tests green

### Running Tests Locally

#### Run All Tests

```bash
# Run all tests (excluding real endpoint tests)
dotnet test BiatecTokensTests/BiatecTokensTests.csproj \
  --filter "FullyQualifiedName!~RealEndpoint"
```

#### Run Tests with Verbose Output

```bash
# Run tests with detailed output
dotnet test BiatecTokensTests/BiatecTokensTests.csproj \
  --verbosity detailed \
  --filter "FullyQualifiedName!~RealEndpoint"
```

#### Run Specific Test Class

```bash
# Run tests from a specific test class
dotnet test BiatecTokensTests/BiatecTokensTests.csproj \
  --filter "FullyQualifiedName~TokenServiceTests"
```

#### Run Tests with Code Coverage

```bash
# Run tests and generate code coverage report
dotnet test BiatecTokensTests/BiatecTokensTests.csproj \
  --collect:"XPlat Code Coverage" \
  --results-directory ./TestResults \
  --filter "FullyQualifiedName!~RealEndpoint"

# Install ReportGenerator (one-time)
dotnet tool install --global dotnet-reportgenerator-globaltool

# Generate HTML coverage report
reportgenerator \
  "-reports:./TestResults/*/coverage.cobertura.xml" \
  "-targetdir:./CoverageReport" \
  "-reporttypes:Html;TextSummary"

# View summary in terminal
cat ./CoverageReport/Summary.txt

# Open HTML report in browser
# On Linux/Mac:
xdg-open ./CoverageReport/index.html
# On Windows:
start ./CoverageReport/index.html
```

### Writing Good Tests

#### Test Structure (AAA Pattern)

Follow the **Arrange-Act-Assert** (AAA) pattern:

```csharp
[Test]
public void MethodName_Scenario_ExpectedBehavior()
{
    // Arrange: Set up test data and dependencies
    var service = new MyService();
    var input = new MyInput { Value = 42 };

    // Act: Execute the behavior being tested
    var result = service.ProcessInput(input);

    // Assert: Verify the expected outcome
    Assert.That(result.Success, Is.True);
    Assert.That(result.Value, Is.EqualTo(42));
}
```

#### Test Naming Convention

- Use descriptive names: `MethodName_Scenario_ExpectedResult`
- Examples:
  - `CreateToken_ValidRequest_ReturnsSuccess`
  - `ValidateRequest_NullName_ThrowsArgumentException`
  - `DeployERC20_InvalidChainId_ReturnsError`

#### Use NUnit Constraints

Prefer the constraint model over classic assertions:

```csharp
// ✅ Good - Use constraint model
Assert.That(result.Success, Is.True);
Assert.That(result.TransactionId, Is.Not.Null);
Assert.That(result.ErrorMessage, Is.Null.Or.Empty);

// ❌ Avoid - Classic assertions
Assert.AreEqual(true, result.Success);
Assert.IsNotNull(result.TransactionId);
```

#### Mock External Dependencies

Use Moq to mock external dependencies:

```csharp
[Test]
public void ServiceMethod_WithMockedDependency_BehavesCorrectly()
{
    // Arrange
    var mockLogger = new Mock<ILogger<MyService>>();
    var mockConfig = new Mock<IOptionsMonitor<MyConfig>>();
    mockConfig.Setup(x => x.CurrentValue).Returns(new MyConfig());
    
    var service = new MyService(mockConfig.Object, mockLogger.Object);

    // Act & Assert
    // ...
}
```

#### Test Edge Cases

Always test:
- Valid inputs (happy path)
- Invalid inputs (null, empty, out of range)
- Boundary conditions
- Error conditions

#### Example Test File

See `BiatecTokensTests/TDDExampleTests.cs` for comprehensive examples of:
- Basic test structure
- Parameterized tests with `[TestCase]`
- Setup and teardown methods
- Mocking dependencies

## Code Coverage Requirements

### Current Coverage Thresholds

**Note**: We are taking an incremental approach to achieving our coverage goals. The current enforced thresholds are:

- **Line Coverage**: ≥ 11% (baseline to prevent regression)
- **Branch Coverage**: ≥ 3% (baseline to prevent regression)

**Target Goals** (to be reached incrementally through community contributions):

- **Line Coverage**: ≥ 80%
- **Branch Coverage**: ≥ 70%

### Coverage Improvement Strategy

We're building code coverage incrementally. Each PR should:

1. **Maintain or improve** current coverage levels
2. **Add tests** for any new code added
3. **Aim to increase** the overall project coverage percentage

When adding new features:
- Write tests FIRST (TDD approach)
- Ensure your new code has >80% coverage
- Don't decrease existing coverage

### What Gets Measured

Coverage is measured for:
- Service layer (`BiatecTokensApi/Services/`)
- Controller layer (`BiatecTokensApi/Controllers/`)
- Repository layer (`BiatecTokensApi/Repositories/`)
- Model validation logic

### What's Excluded

The following are excluded from coverage requirements:
- Generated code (`BiatecTokensApi/Generated/`)
- Program.cs (application startup)
- Model classes (DTOs)
- Configuration classes

### CI Coverage Checks

The CI pipeline automatically:
1. Runs all tests with coverage collection
2. Generates a coverage report
3. Checks coverage thresholds
4. **Fails the build** if thresholds are not met
5. Uploads coverage report as an artifact

You can view the coverage report by:
1. Going to the Actions tab in GitHub
2. Clicking on your PR's workflow run
3. Downloading the "coverage-report" artifact
4. Opening `index.html` in a browser

### Improving Coverage

If your PR doesn't meet coverage requirements or you want to help improve overall coverage:

1. **Identify gaps**: Check the coverage report to see which lines/branches aren't covered
2. **Add tests**: Write new tests targeting uncovered code paths
3. **Test edge cases**: Ensure you're testing error conditions and boundary cases
4. **Mock appropriately**: Use mocks to isolate the code under test

**Areas needing test coverage** (good places to contribute):
- `ASATokenService` - Algorand Standard Asset token creation
- `ARC3TokenService` - ARC3 token with IPFS metadata
- `ARC200TokenService` - ARC200 smart contract tokens  
- `ARC1400TokenService` - ARC1400 security tokens
- Controller validation logic
- Request validation methods

See `BiatecTokensTests/TDDExampleTests.cs` for test patterns and examples.

### Incremental Coverage Goals

We're working toward 80%/70% coverage through incremental improvements:

| Quarter | Line Target | Branch Target | Status |
|---------|-------------|---------------|--------|
| Q1 2026 (Baseline) | 11% | 3% | ✅ Current |
| Q2 2026 | 30% | 15% | 🎯 Next milestone |
| Q3 2026 | 50% | 35% | 📅 Planned |
| Q4 2026 | 65% | 50% | 📅 Planned |
| Q1 2027 | 80% | 70% | 🏆 Goal |

Every contribution that improves coverage brings us closer to our goal!

## Pull Request Process

### Before Submitting

1. **Write tests first** (TDD approach)
2. **Ensure all tests pass locally**:
   ```bash
   dotnet test BiatecTokensTests/BiatecTokensTests.csproj \
     --filter "FullyQualifiedName!~RealEndpoint"
   ```
3. **Check code coverage** maintains or improves baseline (currently ≥11% line, ≥3% branch)
4. **Build successfully**:
   ```bash
   dotnet build BiatecTokensApi.sln --configuration Release
   ```
5. **Follow coding standards** (see below)
6. **Update documentation** if adding new features or changing APIs
7. **Add XML documentation comments** for all public APIs

### Submitting a PR

1. **Push to your fork**
2. **Create a pull request** against the `master` branch
3. **Fill out the PR template** with:
   - Clear description of changes
   - Link to related issue (if any)
   - Testing performed
   - Screenshots (for UI changes)
4. **Tag reviewers** including `@copilot` for automated review

### PR Requirements

All PRs must meet these criteria:

✅ **Required Status Checks** (enforced by CI):
- Build passes
- All tests pass
- Code coverage ≥ 11% lines and ≥ 3% branches (baseline - should improve over time toward 80%/70% target)
- No merge conflicts

✅ **Code Review**:
- At least **1 approval** from a maintainer required
- Address all review comments
- Resolve all conversations

✅ **Quality Standards**:
- Code follows project conventions
- XML documentation added for public APIs
- No compiler warnings introduced
- Security vulnerabilities addressed

### After Approval

Once your PR is approved and all checks pass:
1. A maintainer will merge your PR
2. Your changes will be automatically deployed via CI/CD
3. You'll be credited as a contributor!

## Coding Standards

### C# Conventions

- Follow [C# Coding Conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Use PascalCase for public members, camelCase for private fields
- Enable nullable reference types
- Use explicit types when type is not obvious
- Avoid `var` for non-obvious types

### Documentation

- Add XML documentation comments (`///`) for all public APIs
- Include `<summary>`, `<param>`, `<returns>`, and `<exception>` tags
- Document complex logic with inline comments
- Update README.md for significant feature additions

### Example

```csharp
/// <summary>
/// Creates a new ERC20 mintable token on the specified EVM chain.
/// </summary>
/// <param name="request">The token creation request containing token parameters.</param>
/// <returns>The transaction result including contract address and transaction hash.</returns>
/// <exception cref="ArgumentNullException">Thrown when request is null.</exception>
/// <exception cref="InvalidOperationException">Thrown when chain configuration is not found.</exception>
public async Task<ERC20TokenDeploymentResponse> CreateERC20MintableAsync(
    ERC20MintableTokenDeploymentRequest request)
{
    // Implementation...
}
```

### File Organization

- One class per file
- Organize using folders by feature/layer:
  - `/Controllers` - API endpoints
  - `/Services` - Business logic
  - `/Repositories` - Data access
  - `/Models` - DTOs and data models
  - `/Configuration` - Configuration classes

### Error Handling

- Catch specific exceptions
- Log errors with appropriate context
- Return user-friendly error messages
- Never expose internal details in responses

## Branch Protection Rules

### Required Status Checks

The following checks must pass before merging:

1. ✅ **Build and Test** (`build-and-test` job)
   - Build succeeds
   - All tests pass
   - Code coverage thresholds met

2. ✅ **Code Quality**
   - No new compiler warnings
   - XML documentation present
   - Follows coding standards

### Required Approvals

- **1 approval** from a code owner or maintainer is required
- Approvals automatically dismissed on new commits (if configured)

### Branch Settings

To maintain code quality, the `master` branch has these protections:

- Cannot force push
- Cannot delete the branch
- Status checks must pass before merging
- At least 1 approval required
- Stale approvals dismissed on new commits

**Note**: Branch protection rules are configured in the GitHub repository settings by repository administrators. Contributors cannot change these settings.

## Getting Help

- **Questions?** Open a [GitHub Discussion](https://github.com/scholtz/BiatecTokensApi/discussions)
- **Found a bug?** Open an [Issue](https://github.com/scholtz/BiatecTokensApi/issues)
- **Security concern?** See [SECURITY.md](SECURITY.md) (if it exists) or contact maintainers privately

## Additional Resources

- [.NET Documentation](https://learn.microsoft.com/en-us/dotnet/)
- [NUnit Documentation](https://docs.nunit.org/)
- [Moq Documentation](https://github.com/moq/moq4)
- [Algorand Developer Documentation](https://developer.algorand.org/)
- [Nethereum Documentation](https://docs.nethereum.com/)

## License

By contributing, you agree that your contributions will be licensed under the same license as the project.

---

Thank you for contributing to BiatecTokensApi! 🚀

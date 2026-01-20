# CI Stabilization Fix - Integration Tests

## Problem

The initial implementation of integration tests caused CI failures because:

1. **Network Dependencies**: Tests used `WebApplicationFactory<Program>` which started the actual application
2. **Service Initialization**: Services (ASATokenService, ARC3TokenService, ARC200TokenService, ARC1400TokenService) attempted to connect to external Algorand nodes in their constructors
3. **Blocking Calls**: These services used `.Result` on async calls during DI registration, causing blocking network requests
4. **CI Environment**: GitHub Actions runners couldn't access the external Algorand nodes, causing test failures

## Solution

### 1. Custom WebApplicationFactory with Mocked Services

Created a `CustomWebApplicationFactory` that:
- Removes the original service registrations that require network access
- Replaces them with Moq-based mock implementations
- Configures in-memory test configuration

```csharp
private class CustomWebApplicationFactory : WebApplicationFactory<BiatecTokensApi.Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove services requiring network access
            var descriptorsToRemove = services
                .Where(d => d.ServiceType == typeof(IARC3TokenService) ||
                           d.ServiceType == typeof(IARC200TokenService) ||
                           d.ServiceType == typeof(IARC1400TokenService) ||
                           d.ServiceType == typeof(IASATokenService))
                .ToList();

            foreach (var descriptor in descriptorsToRemove)
            {
                services.Remove(descriptor);
            }

            // Register mock services
            services.AddSingleton(new Mock<IARC3TokenService>().Object);
            services.AddSingleton(new Mock<IARC200TokenService>().Object);
            services.AddSingleton(new Mock<IARC1400TokenService>().Object);
            services.AddSingleton(new Mock<IASATokenService>().Object);
        });
    }
}
```

### 2. Updated CI Workflow

Added test filter to exclude tests that genuinely require network access:

```yaml
--filter "FullyQualifiedName!~RealEndpoint"
```

This ensures:
- Integration tests run without external dependencies
- Tests that need real endpoints (like `IPFSRepositoryRealEndpointTests`) are excluded from CI
- Tests remain deterministic and fast

### 3. Fixed Test Assertions

Updated `API_ShouldRespondToRootRequest` to test Swagger UI availability instead of expecting the root endpoint to exist:

```csharp
[Test]
public async Task API_SwaggerUI_ShouldBeDiscoverable()
{
    var response = await _client.GetAsync("/swagger/index.html");
    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
}
```

## Results

✅ **All 15 integration tests pass**
```
Test Run Successful.
Total tests: 15
     Passed: 15
 Total time: 1.2173 Seconds
```

### Tests Covered
1. ✅ Swagger_Endpoint_ShouldBeAccessible
2. ✅ Swagger_UI_ShouldBeAccessible
3. ✅ ERC20Mintable_Endpoint_ShouldRequireAuthentication
4. ✅ ERC20Preminted_Endpoint_ShouldRequireAuthentication
5. ✅ ASAFungibleToken_Endpoint_ShouldRequireAuthentication
6. ✅ ASANFT_Endpoint_ShouldRequireAuthentication
7. ✅ ASAFNFT_Endpoint_ShouldRequireAuthentication
8. ✅ ARC3FungibleToken_Endpoint_ShouldRequireAuthentication
9. ✅ ARC3NFT_Endpoint_ShouldRequireAuthentication
10. ✅ ARC3FractionalNFT_Endpoint_ShouldRequireAuthentication
11. ✅ ARC200Mintable_Endpoint_ShouldRequireAuthentication
12. ✅ ARC200Preminted_Endpoint_ShouldRequireAuthentication
13. ✅ ARC1400Mintable_Endpoint_ShouldRequireAuthentication
14. ✅ OpenAPI_Schema_ShouldContainAllTokenEndpoints
15. ✅ API_SwaggerUI_ShouldBeDiscoverable

## Benefits

### Deterministic Tests
- No flakiness from network timeouts
- No dependency on external services being available
- Tests run consistently in any environment

### Fast Execution
- No waiting for network calls
- Tests complete in ~1.2 seconds
- CI pipeline is faster

### Following Best Practices
- Tests use mocks for external dependencies (TDD principle)
- Integration tests focus on API contract verification
- Separation of concerns: unit tests vs integration tests vs e2e tests

## What Tests Verify

The integration tests verify:
1. **API Structure**: Endpoints are registered and accessible
2. **Authentication**: ARC-0014 authentication is enforced on protected endpoints
3. **OpenAPI Contract**: Swagger documentation is generated correctly
4. **Discovery**: Swagger UI is available for API exploration

The tests do NOT:
- Make actual blockchain transactions
- Connect to real blockchain nodes
- Test business logic (covered by unit tests)
- Require real authentication tokens

## CI Workflow

The updated workflow:
1. Builds the solution ✅
2. Runs all tests except those requiring real network endpoints ✅
3. Generates OpenAPI specification ✅
4. Publishes test results ✅
5. Uploads OpenAPI artifact ✅
6. Comments on PR with artifact link ✅

## Files Changed

- `.github/workflows/test-pr.yml` - Added test filter
- `BiatecTokensTests/ApiIntegrationTests.cs` - Added mocked services, fixed tests

## Commit

```
commit c1a0549
Fix integration tests to use mocked services and avoid external network dependencies

- Configure WebApplicationFactory with mocked blockchain services
- Remove dependency on external Algorand nodes during test initialization
- Replace ASA/ARC3/ARC200/ARC1400 services with Moq-based stubs
- Update API_ShouldRespondToRootRequest test to check Swagger UI instead
- Filter out RealEndpoint tests in CI workflow (require actual network)
- All 15 integration tests now pass without external dependencies
```

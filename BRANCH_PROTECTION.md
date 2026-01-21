# Branch Protection Configuration

This document provides instructions for repository administrators to configure branch protection rules and required status checks for the BiatecTokensApi repository.

## Overview

Branch protection rules help maintain code quality by enforcing certain conditions before code can be merged into protected branches. This ensures all changes go through proper review and testing processes.

## Required Branch Protection Settings

### For the `master` branch:

1. **Require pull request reviews before merging**
   - Require 1 approval before merging
   - Dismiss stale pull request approvals when new commits are pushed
   - Require review from Code Owners (if CODEOWNERS file exists)

2. **Require status checks to pass before merging**
   - Require branches to be up to date before merging
   - Required status checks:
     - `build-and-test` (from test-pr.yml workflow)
     - All jobs within the test-pr.yml workflow must pass

3. **Require conversation resolution before merging**
   - All conversations on the pull request must be resolved

4. **Do not allow bypassing the above settings**
   - Enforce all configured restrictions for administrators

5. **Require linear history** (Optional but recommended)
   - Prevent merge commits from being pushed to matching branches

## How to Configure Branch Protection

### Step 1: Access Repository Settings

1. Navigate to the repository on GitHub: https://github.com/scholtz/BiatecTokensApi
2. Click on **Settings** (requires admin access)
3. Click on **Branches** in the left sidebar
4. Under "Branch protection rules", click **Add rule** (or edit existing rule for `master`)

### Step 2: Configure Protection Rule for `master` Branch

1. **Branch name pattern**: Enter `master`

2. **Protect matching branches** section:
   
   ☑️ **Require a pull request before merging**
   - ☑️ Require approvals: `1`
   - ☑️ Dismiss stale pull request approvals when new commits are pushed
   - ☑️ Require review from Code Owners (if applicable)
   
   ☑️ **Require status checks to pass before merging**
   - ☑️ Require branches to be up to date before merging
   - Search and select status checks:
     - `build-and-test` (this is the main job from test-pr.yml)
     - Add any other checks that appear after first PR runs
   
   ☑️ **Require conversation resolution before merging**
   
   ☑️ **Require linear history** (optional)
   
   ☑️ **Include administrators** (recommended)
   
   ☑️ **Allow force pushes** - Leave UNCHECKED
   
   ☑️ **Allow deletions** - Leave UNCHECKED

3. Click **Create** or **Save changes**

### Step 3: Verify Configuration

1. Create a test branch and make a small change
2. Open a pull request against `master`
3. Verify that:
   - CI checks run automatically
   - You cannot merge without approval
   - You cannot merge if status checks fail
   - Coverage thresholds are enforced

## Status Checks Overview

The following CI checks must pass before merging:

### 1. Build and Test (`build-and-test` job from test-pr.yml)

This job includes:
- ✅ Code compilation (no build errors)
- ✅ Unit tests execution (all tests pass)
- ✅ Code coverage collection and reporting
- ✅ Coverage threshold validation:
  - Line coverage ≥ 34% (target: 80%)
  - Branch coverage ≥ 28% (target: 70%)
- ✅ OpenAPI specification generation
- ✅ Test results publishing

### 2. Coverage Thresholds

Current coverage requirements (will be increased incrementally):
- **Line Coverage**: Minimum 34% (target: 80%)
- **Branch Coverage**: Minimum 28% (target: 70%)

Pull requests that decrease coverage below these thresholds will fail the build.

### 3. Coverage Reporting

After each PR build:
- Coverage report is generated and uploaded as an artifact
- Coverage summary is displayed in the PR checks
- Detailed HTML report is available for download

## Dependabot Configuration

Automated dependency updates are configured via `.github/dependabot.yml`:
- NuGet packages (weekly updates)
- GitHub Actions (weekly updates)
- Docker base images (weekly updates)
- PRs automatically created with security updates
- Requires manual review and approval before merging

## Code Review Guidelines

When reviewing pull requests:

1. **Code Quality**
   - Follows C# coding conventions
   - Includes XML documentation for public APIs
   - No unnecessary code duplication
   - Proper error handling

2. **Testing**
   - New functionality includes unit tests
   - Tests follow AAA pattern (Arrange, Act, Assert)
   - Edge cases are covered
   - No tests are removed or disabled without justification

3. **Coverage**
   - Coverage should not decrease
   - Aim to improve coverage with each PR
   - Focus on testing critical business logic

4. **Security**
   - No secrets or credentials committed
   - Input validation is present
   - Security best practices followed

5. **Documentation**
   - README updated if needed
   - API documentation updated
   - Breaking changes clearly documented

## Troubleshooting

### Status check not appearing

If the required status check doesn't appear in the branch protection settings:
1. Merge at least one PR to `master` to trigger the workflow
2. Wait for the workflow to complete
3. Return to branch protection settings and refresh
4. The status check should now be available to select

### Unable to merge despite passing checks

If all checks pass but merge is still blocked:
1. Verify the PR has required approvals (1 approval needed)
2. Check that all conversations are resolved
3. Ensure the branch is up to date with `master`
4. Verify you have write access to the repository

### Coverage threshold failures

If your PR fails coverage thresholds:
1. Review the coverage report artifact
2. Identify untested code paths
3. Add unit tests for new functionality
4. Ensure tests exercise error cases and edge conditions
5. Re-run the checks after adding tests

## Incremental Coverage Improvement Plan

The project is on a path to reach target coverage levels:

**Phase 1** (Current): 34% line / 28% branch ✅
- Basic validation tests
- Controller endpoint tests
- Error handling tests

**Phase 2** (Next milestone): 50% line / 40% branch
- Service layer unit tests with mocked dependencies
- Repository tests
- Additional edge cases

**Phase 3** (Target): 80% line / 70% branch
- Comprehensive service tests
- Integration test scenarios
- Full error path coverage
- Boundary condition tests

## Maintaining Coverage

To maintain and improve coverage:

1. **Every PR should include tests** for new functionality
2. **Don't remove tests** without replacing them
3. **Run tests locally** before pushing:
   ```bash
   dotnet test BiatecTokensTests/BiatecTokensTests.csproj \
     --collect:"XPlat Code Coverage" \
     --filter "FullyQualifiedName!~RealEndpoint"
   ```
4. **Review coverage reports** to identify gaps
5. **Focus on critical paths** first (authentication, token creation, validation)

## Additional Resources

- [GitHub Branch Protection Documentation](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-protected-branches/about-protected-branches)
- [Required Status Checks](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-protected-branches/about-protected-branches#require-status-checks-before-merging)
- [CODEOWNERS File](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/about-code-owners)
- [CONTRIBUTING.md](./CONTRIBUTING.md) - Developer contribution guidelines

## Questions or Issues?

If you encounter any problems with branch protection or CI checks:
1. Check the [CI workflow file](.github/workflows/test-pr.yml)
2. Review test execution logs in GitHub Actions
3. Consult the CONTRIBUTING.md for testing guidelines
4. Open an issue on GitHub with details

---

**Note for Repository Administrators**: This configuration must be set up through the GitHub web interface by a user with admin access to the repository. It cannot be configured through code or pull requests.

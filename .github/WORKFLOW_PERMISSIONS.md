# GitHub Actions Workflow Permission Model Documentation

**Document Version**: 1.0  
**Last Updated**: 2026-02-09  
**Applies To**: `.github/workflows/test-pr.yml` and related workflows

## Overview

This document defines the permission boundaries for GitHub Actions workflows in the BiatecTokensApi repository. It explains what each permission grants, why it's needed, and how the permission model handles different PR types securely.

## Permission Model

### Declared Permissions

Our workflows declare these permissions explicitly:

```yaml
permissions:
  contents: read        # Clone repository and read code
  pull-requests: write  # Comment on PRs and update PR metadata
  issues: write         # Comment on PRs (PRs are special issues)
  checks: write         # Update check run status
```

### Actual Permissions by PR Type

GitHub enforces different permission levels based on PR origin:

| PR Type | contents | pull-requests | issues | checks | Notes |
|---------|----------|---------------|--------|--------|-------|
| **Dependabot** | read | **read** | **read** | **read** | Downgraded by GitHub for security |
| **Fork** | read | **read** | **read** | read | Limited by GitHub for security |
| **Trusted Branch** | read | write | write | write | Full declared permissions |
| **Protected Branch** | read | write | write | write | Full declared permissions |

**Key Insight**: We declare `write` permissions, but GitHub automatically downgrades them to `read` for untrusted sources (Dependabot, forks). This is a security feature, not a bug.

## Why Each Permission Is Needed

### `contents: read`

**Purpose**: Access repository files to run tests

**Required For**:
- Checking out code (`actions/checkout`)
- Reading source files
- Running build and test commands
- Accessing workflow configuration

**Security**: Read-only prevents workflow from modifying code. This permission is safe for all PR types.

**What It Does NOT Allow**:
- ❌ Commit code
- ❌ Push changes
- ❌ Modify repository settings
- ❌ Delete files from repository

### `pull-requests: write`

**Purpose**: Post comments on PRs with test results and coverage reports

**Required For**:
- Posting test summary comments
- Posting coverage reports
- Posting OpenAPI specification links
- Updating PR description (future feature)

**Security**: 
- Downgraded to `read` for Dependabot/fork PRs
- Only works for trusted contributor PRs
- Cannot modify PR source code, only metadata

**What It Does**:
- ✅ Create comments on PR conversations
- ✅ Add labels to PRs (if we implement)
- ✅ Request reviewers (if we implement)

**What It Does NOT Allow**:
- ❌ Modify PR source code
- ❌ Merge PRs
- ❌ Close PRs
- ❌ Modify repository settings

### `issues: write`

**Purpose**: Comment on PRs (PRs are technically issues in GitHub API)

**Required For**:
- Same as `pull-requests: write`
- GitHub treats PRs as special issues
- Both permissions needed for reliable PR commenting

**Security**: Same as `pull-requests: write`

### `checks: write`

**Purpose**: Update GitHub Check Runs status

**Required For**:
- Publishing test results as check runs
- Updating status badges
- Setting build status (future feature)

**Security**: 
- Only affects check run metadata
- Cannot modify actual code or bypass protections
- Downgraded for Dependabot PRs

**What It Does**:
- ✅ Create/update check runs
- ✅ Set check run status (success/failure)
- ✅ Add check run annotations

**What It Does NOT Allow**:
- ❌ Override required checks
- ❌ Bypass branch protection
- ❌ Merge PRs

## Permission Handling by PR Type

### Dependabot PRs

**Scenario**: Dependabot creates PR with dependency updates

**GitHub Behavior**:
- Automatically downgrades all `write` permissions to `read`
- This is a security measure to prevent supply chain attacks
- Cannot be overridden by workflow configuration

**Our Implementation**:
```yaml
- name: Comment PR with OpenAPI artifact link
  # Skip comment for dependabot - they don't have write permissions
  if: github.event_name == 'pull_request' && github.actor != 'dependabot[bot]' && always()
  uses: actions/github-script@v8
  with:
    script: |
      try {
        await github.rest.issues.createComment({...});
      } catch (error) {
        // Graceful failure - expected for dependabot
        console.log('Unable to comment on PR:', error.message);
        console.log('This is expected for dependabot PRs.');
      }
```

**Test Evidence**:
- ✅ Build succeeds (read permission sufficient)
- ✅ Tests run (read permission sufficient)
- ✅ Artifacts upload (uses separate token)
- ⚠️  Comments skipped (write permission not available)
- ✅ Workflow shows success (graceful degradation)

### Fork PRs

**Scenario**: External contributor forks repo and creates PR

**GitHub Behavior**:
- Downgrades `write` permissions to `read` for security
- Prevents malicious PRs from modifying base repository
- Standard open-source security practice

**Our Implementation**:
Same as Dependabot - comment step skips gracefully

**Test Evidence**:
- ✅ Tests run on fork code
- ✅ Results available in artifacts
- ⚠️  Comments may not post (expected)
- ✅ Workflow completes successfully

### Trusted Branch PRs

**Scenario**: Internal team member creates PR from branch in main repo

**GitHub Behavior**:
- Full declared permissions granted
- Write operations succeed
- Standard workflow execution

**Our Implementation**:
Full functionality - comments, checks, artifacts all work

**Test Evidence**:
- ✅ Build succeeds
- ✅ Tests run
- ✅ Comments post to PR
- ✅ Artifacts upload
- ✅ Check runs update
- ✅ Workflow shows success

## Security Boundaries

### What Workflows CAN Do

**With Read Permissions** (all PR types):
- ✅ Clone and read repository code
- ✅ Run builds and tests
- ✅ Upload artifacts
- ✅ Write to GITHUB_STEP_SUMMARY
- ✅ Log output to console

**With Write Permissions** (trusted PRs only):
- ✅ Post comments to PR conversations
- ✅ Create/update check runs
- ✅ Add labels to PRs
- ✅ Update PR metadata (not code)

### What Workflows CANNOT Do

**Never Allowed** (regardless of permissions):
- ❌ Bypass required status checks
- ❌ Override branch protection rules
- ❌ Merge PRs without approval
- ❌ Modify files in protected branches
- ❌ Access repository secrets (except GITHUB_TOKEN)
- ❌ Delete repository data
- ❌ Change repository settings

**Not Allowed for Dependabot/Fork PRs**:
- ❌ Post PR comments
- ❌ Update PR labels
- ❌ Modify PR metadata
- ❌ Create GitHub releases

## Graceful Degradation Strategy

### Design Philosophy

**Principle**: Core functionality (build + test) should succeed regardless of permissions. Optional features (comments) should fail gracefully without blocking the workflow.

**Implementation Pattern**:

```yaml
- name: Essential step (never skip)
  run: |
    # Core functionality - must succeed
    dotnet build
    dotnet test

- name: Optional step (may skip)
  if: github.actor != 'dependabot[bot]'  # Skip for restricted PRs
  continue-on-error: true                 # Don't block workflow
  run: |
    # Optional functionality - nice to have
    post_comment()
```

### Graceful Failure Hierarchy

**Level 1: Must Succeed** (workflow fails if these fail):
- Repository checkout
- Dependency restoration
- Build compilation
- Test execution
- Coverage calculation

**Level 2: Should Succeed** (workflow succeeds even if these fail):
- Coverage report generation
- OpenAPI spec generation
- Artifact uploads

**Level 3: Nice to Have** (explicitly skipped or allowed to fail):
- PR comments
- Status badge updates
- Optional notifications

## Validation and Testing

### Manual Validation

**Dependabot PR Test**:
1. Wait for Dependabot PR (e.g., PR #279)
2. Verify workflow runs
3. Check logs show "skipping comment" message
4. Verify workflow shows success
5. Verify artifacts contain results

**Fork PR Test**:
1. Create test fork
2. Submit PR from fork
3. Verify workflow runs with limited permissions
4. Check graceful failure handling

**Trusted PR Test**:
1. Create PR from feature branch
2. Verify full functionality
3. Check comments appear
4. Verify all artifacts upload

### Automated Validation

**Workflow**: `.github/workflows/validate-permissions.yml`

**Tests**:
- ✅ Detects PR type (dependabot/fork/trusted)
- ✅ Verifies read permissions work for all types
- ✅ Confirms write operations skip for restricted PRs
- ✅ Validates graceful failure handling
- ✅ Generates summary report

**Usage**:
Runs automatically on all PRs. Check workflow summary for validation results.

## Troubleshooting

### Symptom: Comment step fails with 403 error

**Diagnosis**: Dependabot or fork PR with downgraded permissions

**Solution**: This is expected behavior. Verify:
1. Check if `github.actor` is `dependabot[bot]`
2. Check if PR is from a fork
3. Verify workflow still shows success overall
4. Confirm artifacts uploaded successfully

**Action**: No action needed - working as designed

### Symptom: Workflow fails on trusted PR

**Diagnosis**: Actual permission issue or workflow bug

**Solution**:
1. Check workflow logs for actual error
2. Verify repository settings haven't changed
3. Confirm GITHUB_TOKEN is available
4. Review recent workflow changes

**Action**: Investigation needed - this is not expected

### Symptom: No status checks appear on PR

**Diagnosis**: Workflow not triggered (wrong branch pattern)

**Solution**:
1. Check workflow `on:` triggers
2. Verify PR targets correct branch
3. Check if workflow is enabled
4. Look for workflow run in Actions tab

**Action**: Update workflow triggers if needed

## Best Practices

### DO

✅ Declare minimum required permissions explicitly  
✅ Skip optional steps for restricted PRs  
✅ Use try-catch for write operations  
✅ Log clear messages when skipping steps  
✅ Upload artifacts as alternative to comments  
✅ Test with all PR types before deploying  

### DON'T

❌ Request broader permissions than needed  
❌ Assume write permissions always available  
❌ Fail workflow on comment errors  
❌ Try to work around GitHub security restrictions  
❌ Use third-party tokens to bypass limits  
❌ Ignore permission errors in logs  

## References

### GitHub Documentation

- [GitHub Actions Permissions](https://docs.github.com/en/actions/security-guides/automatic-token-authentication)
- [Dependabot Permissions](https://docs.github.com/en/code-security/dependabot/working-with-dependabot/automating-dependabot-with-github-actions)
- [Fork Pull Request Permissions](https://docs.github.com/en/actions/security-guides/security-hardening-for-github-actions#using-secrets)
- [GITHUB_TOKEN Permissions](https://docs.github.com/en/actions/security-guides/automatic-token-authentication#permissions-for-the-github_token)

### Internal Documentation

- `ISSUE_DEPENDABOT_WORKFLOW_PERMISSIONS_FIX.md` - Business context and requirements
- `DEPENDENCY_UPDATE_VERIFICATION_PR_279.md` - Example verification process
- `.github/copilot-instructions.md` - Dependency update procedures
- `.github/workflows/validate-permissions.yml` - Automated permission testing

## Change Log

### 2026-02-09 - Initial Version

- Documented permission model for test-pr.yml workflow
- Defined security boundaries for each PR type
- Established graceful degradation strategy
- Created validation and testing procedures
- Published troubleshooting guide

---

**Document Owner**: DevOps Team  
**Reviewers**: Security Team, Product Owner  
**Next Review**: 2026-03-09 (or after major workflow changes)  
**Questions**: Create issue with label `workflow-permissions`

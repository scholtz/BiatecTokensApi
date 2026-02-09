# GitHub Actions Workflow Permissions - Technical Documentation

**Last Updated**: 2026-02-09  
**Version**: 1.0  
**Owner**: Infrastructure Team

## Overview

This document explains the permission model for GitHub Actions workflows in the BiatecTokensApi repository, specifically how we handle the read-only token restrictions on Dependabot PRs while maintaining full CI/CD functionality.

## Problem Statement

### Background

GitHub Actions provides workflows with an automatic `GITHUB_TOKEN` that has different permission levels depending on the PR source:

| PR Source | Token Permissions | Reason |
|-----------|------------------|--------|
| **Trusted contributors** | Read + Write | User has write access to repository |
| **Dependabot** | **Read-only** | Security restriction (Dependabot is automated) |
| **Forks** | Read-only | Security restriction (fork owner is external) |

### The Issue

Our `test-pr.yml` workflow posts PR comments and publishes test results after test execution completes. When the PR is from Dependabot:

1. ✅ All tests execute successfully
2. ✅ Build completes without errors
3. ❌ Workflow attempts to post "✅ CI checks passed!" comment
4. ❌ GitHub API returns HTTP 403 "Resource not accessible by integration"
5. ❌ Workflow fails and shows "failed" status
6. ❌ Engineers must manually verify tests actually passed

**Impact**: False "failed" status blocks safe dependency merges and wastes 15-30 minutes per Dependabot PR.

## Solution Architecture

### Design Principles

1. **Graceful Degradation**: Tests must run even if comment/check publishing fails
2. **Fail-Safe**: Permission errors should not fail the entire workflow
3. **Clear Signal**: CI status should reflect test results, not permission issues
4. **Deterministic**: Behavior should be predictable and well-documented

### Implementation Strategy

#### 1. Skip Optional Steps for Dependabot

**Approach**: Detect Dependabot PRs and skip steps that require write permissions.

```yaml
- name: Comment PR with results
  if: github.actor != 'dependabot[bot]' && github.event_name == 'pull_request' && always()
  continue-on-error: true
  uses: actions/github-script@v8
  with:
    script: |
      # Comment posting logic
```

**How It Works**:
- `github.actor != 'dependabot[bot]'`: Detects Dependabot PRs by actor name
- Step is **skipped entirely** for Dependabot PRs
- Human contributor PRs execute normally

#### 2. Graceful Error Handling

**Approach**: Use `continue-on-error: true` to prevent workflow failure on permission errors.

```yaml
- name: Publish test results
  uses: EnricoMi/publish-unit-test-result-action@v2
  if: always()
  continue-on-error: true
  with:
    files: '**/test-results.trx'
```

**How It Works**:
- Step executes for all PR types
- If permission denied (403), step fails but workflow continues
- Workflow status reflects test results, not publishing status

#### 3. Expand Branch Triggers for Stacked PRs

**Approach**: Include `dependabot/**` and `copilot/**` branches in PR triggers.

```yaml
on:
  pull_request:
    branches:
      - master
      - main
      - 'dependabot/**'  # Stacked PRs on Dependabot branches
      - 'copilot/**'     # Stacked PRs on Copilot branches
```

**How It Works**:
- PRs targeting Dependabot branches (e.g., workflow fixes) now trigger CI
- Enables validation of infrastructure changes before merging to master
- Maintains backward compatibility with existing triggers

## Permission Model

### Actor Types and Permissions

| Actor Type | Example | GITHUB_TOKEN Permissions | Comment Step | Publish Results |
|------------|---------|-------------------------|--------------|-----------------|
| **Repository Member** | `user123` | `pull-requests: write`<br>`issues: write` | ✅ Executes | ✅ Executes |
| **Dependabot** | `dependabot[bot]` | `contents: read`<br>`pull-requests: read` | ⏭️ Skipped | 🔄 Executes (may fail gracefully) |
| **Fork Contributor** | `external-user` | `contents: read`<br>`pull-requests: read` | 🔄 Executes (may fail gracefully) | 🔄 Executes (may fail gracefully) |
| **GitHub Actions Bot** | `github-actions[bot]` | `pull-requests: write`<br>`issues: write` | ✅ Executes | ✅ Executes |

**Legend**:
- ✅ Executes normally
- ⏭️ Skipped intentionally
- 🔄 Executes with error handling (may fail, workflow continues)

### Workflow Steps Risk Assessment

| Step | Requires Write | Dependabot Behavior | Fork PR Behavior |
|------|---------------|---------------------|------------------|
| Checkout code | No | ✅ Success | ✅ Success |
| Restore dependencies | No | ✅ Success | ✅ Success |
| Build solution | No | ✅ Success | ✅ Success |
| Run tests | No | ✅ Success | ✅ Success |
| Generate coverage | No | ✅ Success | ✅ Success |
| Upload artifacts | No | ✅ Success | ✅ Success |
| **Publish test results** | **Yes** (creates checks) | 🔄 May fail (graceful) | 🔄 May fail (graceful) |
| **Comment PR** | **Yes** (writes comment) | ⏭️ Skipped | 🔄 May fail (graceful) |

**Key Insight**: Only the last 2 steps (optional reporting) have permission issues. Core functionality (build + test) always succeeds.

## Implementation Details

### File: `.github/workflows/test-pr.yml`

#### Change 1: Expand Pull Request Triggers

```yaml
on:
  pull_request:
    branches:
      - master
      - main
      - 'dependabot/**'  # NEW: Enable CI on stacked PRs
      - 'copilot/**'     # NEW: Enable CI on stacked PRs
```

**Rationale**: Stacked PRs (e.g., workflow fixes targeting Dependabot branches) need CI validation before merging to master.

#### Change 2: Skip Comment Step for Dependabot

**Before**:
```yaml
- name: Comment PR with OpenAPI artifact link
  if: github.event_name == 'pull_request' && always()
  uses: actions/github-script@v8
```

**After**:
```yaml
- name: Comment PR with OpenAPI artifact link
  if: github.actor != 'dependabot[bot]' && github.event_name == 'pull_request' && always()
  continue-on-error: true
  uses: actions/github-script@v8
```

**Changes**:
- Added `github.actor != 'dependabot[bot]'` condition
- Added `continue-on-error: true` safety net
- Dependabot PRs: Step skipped entirely (no 403 error)
- Fork PRs: Step executes, may fail gracefully (workflow continues)

#### Change 3: Add Error Handling to Test Results Publishing

**Before**:
```yaml
- name: Publish test results
  uses: EnricoMi/publish-unit-test-result-action@v2
  if: always()
```

**After**:
```yaml
- name: Publish test results
  uses: EnricoMi/publish-unit-test-result-action@v2
  if: always()
  continue-on-error: true
```

**Changes**:
- Added `continue-on-error: true`
- If publishing fails (permission denied), workflow continues
- Test artifacts still uploaded (separate step, no write permission needed)

### File: `.github/workflows/validate-permissions.yml`

New workflow that validates the permission handling logic:

**Purpose**:
- Automated validation of workflow syntax
- Checks for required permission handling patterns
- Simulates different actor types
- Validates documentation completeness

**Triggers**:
- Manual dispatch (workflow_dispatch)
- When `test-pr.yml` or validation workflow changes

**Validations**:
1. ✅ Dependabot skip logic exists in test-pr.yml
2. ✅ `continue-on-error: true` on comment step
3. ✅ Branch triggers include `dependabot/**` and `copilot/**`
4. ✅ Documentation files exist
5. ✅ Process documentation includes Dependabot section

## Testing Guide

### Manual Testing Scenarios

#### Scenario 1: Dependabot PR (Read-Only Token)

**Setup**:
1. Wait for Dependabot to create a dependency update PR
2. Or manually trigger Dependabot: `@dependabot rebase`

**Expected Behavior**:
- ✅ Workflow triggers automatically
- ✅ Tests execute and complete
- ✅ Coverage report generated
- ✅ Artifacts uploaded
- ⏭️ PR comment step skipped (not executed)
- ✅ Workflow status shows "passed" (if tests pass)

**Validation**:
```bash
# Check workflow logs
1. Navigate to Actions tab
2. Find the Dependabot PR workflow run
3. Verify "Comment PR" step shows "Skipped" (not "Failed")
4. Verify workflow conclusion is "success"
```

#### Scenario 2: Human Contributor PR (Write Token)

**Setup**:
1. Create a branch from master
2. Make a code change
3. Open PR targeting master

**Expected Behavior**:
- ✅ Workflow triggers automatically
- ✅ Tests execute and complete
- ✅ Coverage report generated
- ✅ Test results published as checks
- ✅ PR comment posted with OpenAPI artifact link
- ✅ Workflow status shows actual test result

**Validation**:
```bash
# Check PR
1. PR should have comment from github-actions[bot]
2. PR checks should show test results
3. Workflow status should match test outcome
```

#### Scenario 3: Stacked PR (Copilot/* Branch)

**Setup**:
1. Create a branch from a Dependabot branch
2. Make workflow or infrastructure changes
3. Open PR targeting the Dependabot branch

**Expected Behavior**:
- ✅ Workflow triggers (new capability!)
- ✅ Tests execute and validate changes
- ✅ Can verify workflow fixes before merging to master

**Validation**:
```bash
# Check Actions tab
1. Verify workflow run exists for the stacked PR
2. Verify tests executed
3. Confirm CI status is accurate
```

### Automated Testing

Run the validation workflow:

```bash
# Trigger validation workflow manually
gh workflow run validate-permissions.yml

# Check validation results
gh run list --workflow=validate-permissions.yml
gh run view <run-id>
```

**Expected Output**:
- ✅ All permission handling validations passed
- ✅ Workflow triggers validated
- ✅ Documentation validated
- ✅ Permission detection simulation successful

## Troubleshooting

### Issue: Workflow doesn't trigger on stacked PR

**Symptoms**:
- Created PR targeting `dependabot/nuget/...` branch
- No workflow run appears in Actions tab
- CI checks missing on PR

**Diagnosis**:
```bash
# Check workflow triggers
cat .github/workflows/test-pr.yml | grep -A 10 "pull_request:"
```

**Fix**:
- Ensure `dependabot/**` and `copilot/**` are in the branches list
- Push a new commit to re-trigger workflow

### Issue: Comment step still failing on Dependabot PR

**Symptoms**:
- Dependabot PR shows "failed" workflow status
- Logs show HTTP 403 error on comment step
- Tests passed, but workflow failed

**Diagnosis**:
```bash
# Check workflow step conditions
cat .github/workflows/test-pr.yml | grep -A 3 "Comment PR"
```

**Fix**:
- Verify `if: github.actor != 'dependabot[bot]'` condition exists
- Verify `continue-on-error: true` is present
- Check actor name in logs matches `dependabot[bot]` exactly

### Issue: Test results not published

**Symptoms**:
- Tests pass, but no test results check appears
- No test summary in PR

**Expected Behavior**:
- Test results publishing **may fail** on Dependabot/fork PRs (permission issue)
- This is **acceptable** - test artifacts are still uploaded
- Workflow should not fail (due to `continue-on-error: true`)

**Workaround**:
```bash
# Download and view test artifacts manually
1. Go to Actions → Workflow run
2. Download "coverage-report" artifact
3. Open CoverageReport/index.html
```

## Security Considerations

### Why Read-Only for Dependabot?

GitHub restricts Dependabot to read-only tokens because:
1. **Automated PRs**: Dependabot creates PRs without human review
2. **Supply Chain Risk**: Compromised dependency could inject malicious code
3. **Least Privilege**: Limits blast radius if Dependabot is compromised

**Our Approach**: Accept the restriction, design workflow to degrade gracefully.

### Fork PR Security

Fork PRs also have read-only tokens to prevent:
- Malicious PR authors from accessing repository secrets
- Unauthorized modification of repository state
- Privilege escalation attacks

**Our Approach**: Same graceful degradation as Dependabot PRs.

### Permissions We DO NOT Change

We intentionally **do not** request additional permissions because:
- ❌ `pull-requests: write` for Dependabot would violate GitHub security model
- ❌ Granting broad permissions increases attack surface
- ✅ Current approach: Minimal permissions, graceful degradation

## Rollback Plan

If the workflow changes cause issues:

### Quick Rollback
```bash
# Revert to previous version
git revert <commit-sha>
git push origin master
```

### Gradual Rollback
```yaml
# Remove stacked PR triggers (keep permission handling)
on:
  pull_request:
    branches:
      - master
      - main
      # Comment out if problematic:
      # - 'dependabot/**'
      # - 'copilot/**'
```

### Full Rollback
```yaml
# Remove all changes (back to original behavior)
- name: Comment PR
  if: github.event_name == 'pull_request' && always()
  # Remove: continue-on-error: true
  # Remove: github.actor check
```

**Risk**: Reverts to false "failed" status on Dependabot PRs.

## Future Improvements

### Auto-Merge for Dependabot (Phase 2)

Once workflow is stable:
```yaml
# .github/workflows/auto-merge-dependabot.yml
- name: Auto-merge Dependabot PR
  if: |
    github.actor == 'dependabot[bot]' &&
    github.event.pull_request.mergeable_state == 'clean' &&
    contains(github.event.pull_request.labels.*.name, 'dependencies')
  run: gh pr merge --auto --squash "$PR_URL"
```

### Enhanced Validation (Phase 3)

- Dependency vulnerability scanning (GitHub Advanced Security)
- Breaking change detection
- Performance regression tests
- Automated changelog generation

## References

### GitHub Documentation
- [Automatic token authentication](https://docs.github.com/en/actions/security-guides/automatic-token-authentication)
- [Permissions for the GITHUB_TOKEN](https://docs.github.com/en/actions/security-guides/automatic-token-authentication#permissions-for-the-github_token)
- [Automating Dependabot with GitHub Actions](https://docs.github.com/en/code-security/dependabot/working-with-dependabot/automating-dependabot-with-github-actions)

### Related Documentation
- `ISSUE_DEPENDABOT_WORKFLOW_PERMISSIONS_FIX.md`: Business case and requirements
- `DEPENDENCY_UPDATE_PR_279_RESOLUTION.md`: Root cause analysis
- `.github/copilot-instructions.md`: Process documentation

### Example PRs
- PR #279: Original Dependabot PR that exposed the issue
- This PR: Workflow fix implementation

---

**Version History**:
- v1.0 (2026-02-09): Initial documentation
- Author: Infrastructure Team
- Reviewers: Product Owner, Engineering Team

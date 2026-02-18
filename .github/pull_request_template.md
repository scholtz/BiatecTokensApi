## Issue Reference

<!-- Link to the issue this PR addresses -->
Closes #<!-- issue number -->

**Related Issues**: <!-- List any related issues -->

**Roadmap Alignment**: <!-- Reference section from business-owner-roadmap.md -->
- Phase: <!-- e.g., "Phase 1: MVP Foundation" -->
- Completion Impact: <!-- e.g., "Increases 'Backend Token Creation & Authentication' from 50% → 65%" -->

---

## Summary

<!-- Provide a clear, concise description of what this PR changes and why -->

### Problem Statement
<!-- What problem does this PR solve? -->

### Solution Approach
<!-- How does this PR solve the problem? -->

---

## Business Value

### Revenue Impact
<!-- How does this change affect revenue? Use specific metrics -->
- **ARR Impact**: <!-- e.g., "+$50K ARR from 100 additional customers @ $42/month average" -->
- **Conversion Impact**: <!-- e.g., "+5% conversion rate improvement" -->
- **Customer Impact**: <!-- e.g., "Enables enterprise customers requiring compliance" -->

### Cost Reduction
<!-- How does this change reduce operational costs? -->
- **Engineering Efficiency**: <!-- e.g., "-20 hours/month debugging time" -->
- **Support Reduction**: <!-- e.g., "-$5K/year from reduced support tickets" -->
- **Infrastructure Savings**: <!-- e.g., "-$2K/month from optimized queries" -->

### Risk Mitigation
<!-- What risks does this change reduce or eliminate? -->
- **Regulatory Risk**: <!-- e.g., "Reduces MICA compliance audit risk" -->
- **Security Risk**: <!-- e.g., "Eliminates SQL injection vulnerability" -->
- **Operational Risk**: <!-- e.g., "Reduces deployment failure rate from 15% → 2%" -->

**Total Business Value**: <!-- Summary: e.g., "+$50K ARR, -$60K costs, ~$500K risk mitigation" -->

---

## Risk Assessment

### Implementation Risks
<!-- What could go wrong during implementation? -->
- **Risk**: <!-- Describe risk -->
  - **Likelihood**: <!-- High/Medium/Low -->
  - **Impact**: <!-- High/Medium/Low -->
  - **Mitigation**: <!-- How you're addressing it -->

### Deployment Risks
<!-- What could go wrong during deployment? -->
- **Risk**: <!-- Describe risk -->
  - **Likelihood**: <!-- High/Medium/Low -->
  - **Impact**: <!-- High/Medium/Low -->
  - **Mitigation**: <!-- How you're addressing it -->

### Operational Risks
<!-- What could go wrong in production? -->
- **Risk**: <!-- Describe risk -->
  - **Likelihood**: <!-- High/Medium/Low -->
  - **Impact**: <!-- High/Medium/Low -->
  - **Mitigation**: <!-- How you're addressing it -->

**Overall Risk Level**: <!-- High/Medium/Low -->

---

## Test Coverage Matrix

### Unit Tests
<!-- List new or modified unit tests -->
- [ ] **Test File**: `path/to/TestFile.cs`
  - **Tests Added**: <!-- Number of tests -->
  - **Coverage**: <!-- What they cover -->
  - **Result**: <!-- Passing/Failing -->

### Integration Tests
<!-- List new or modified integration tests -->
- [ ] **Test File**: `path/to/IntegrationTests.cs`
  - **Tests Added**: <!-- Number of tests -->
  - **Scenarios**: <!-- What scenarios are covered -->
  - **Result**: <!-- Passing/Failing -->

### E2E Tests
<!-- List new or modified E2E tests -->
- [ ] **Test File**: `path/to/E2ETests.cs`
  - **Tests Added**: <!-- Number of tests -->
  - **User Journey**: <!-- What user journey is validated -->
  - **Result**: <!-- Passing/Failing -->

### Test Execution Summary
```bash
# Command to run tests
dotnet test --filter "FullyQualifiedName~YourTests"

# Result
Total: X, Passed: Y, Failed: Z, Skipped: W
```

**Total New Tests**: <!-- Number -->  
**Overall Pass Rate**: <!-- Percentage -->

---

## Acceptance Criteria Traceability

<!-- Map each acceptance criteria from the issue to implementation evidence -->

### AC1: <!-- Criterion description -->
- **Status**: ✅ Satisfied / ⏳ Partial / ❌ Not Satisfied
- **Evidence**: <!-- Code files, line numbers, test results -->
- **Verification**: <!-- How to verify it works -->

### AC2: <!-- Criterion description -->
- **Status**: ✅ Satisfied / ⏳ Partial / ❌ Not Satisfied
- **Evidence**: <!-- Code files, line numbers, test results -->
- **Verification**: <!-- How to verify it works -->

<!-- Repeat for all acceptance criteria -->

---

## Code Changes Summary

### Files Modified
<!-- List files modified with brief description of changes -->
- `path/to/file1.cs`: <!-- What changed -->
- `path/to/file2.cs`: <!-- What changed -->

### Files Added
<!-- List new files -->
- `path/to/newfile.cs`: <!-- Purpose -->

### Files Deleted
<!-- List deleted files -->
- `path/to/oldfile.cs`: <!-- Why deleted -->

### Breaking Changes
<!-- List any breaking changes -->
- [ ] **Breaking Change**: <!-- Description -->
  - **Impact**: <!-- Who/what is affected -->
  - **Migration Path**: <!-- How to adapt -->

**Total LOC Changed**: <!-- Approximate lines of code changed -->

---

## CI Quality Evidence

### CI Test Results
<!-- Provide link to CI run or paste results -->
- **Build Status**: <!-- Pass/Fail -->
- **Test Results**: <!-- Pass/Fail with counts -->
- **Coverage**: <!-- Coverage percentage -->
- **Warnings**: <!-- Number of warnings -->
- **Errors**: <!-- Number of errors -->

### CI Repeatability
<!-- Evidence of multiple successful runs -->
| Run | Date | Status | Tests Passed | Duration |
|-----|------|--------|--------------|----------|
| 1 | YYYY-MM-DD | ✅ Pass | X/Y | Zm Ns |
| 2 | YYYY-MM-DD | ✅ Pass | X/Y | Zm Ns |
| 3 | YYYY-MM-DD | ✅ Pass | X/Y | Zm Ns |

**Observation**: <!-- e.g., "Deterministic results across 3 runs" -->

---

## Security Considerations

### Security Scan Results
<!-- Results from CodeQL or other security scanners -->
- **CodeQL**: <!-- Pass/Fail, number of alerts -->
- **Dependency Vulnerabilities**: <!-- None/List them -->
- **Secrets Detection**: <!-- Pass/Fail -->

### Security Best Practices Checklist
- [ ] No hardcoded secrets or credentials
- [ ] All user inputs sanitized (LoggingHelper.SanitizeLogInput)
- [ ] SQL injection prevention (parameterized queries)
- [ ] Authentication/authorization properly enforced
- [ ] Sensitive data encrypted at rest (AES-256-GCM)
- [ ] Secure communication (HTTPS only)
- [ ] Rate limiting implemented where appropriate
- [ ] CORS configured securely
- [ ] Error messages don't leak sensitive information

---

## Documentation Updates

### Documentation Added/Modified
<!-- List documentation changes -->
- [ ] `README.md`: <!-- What changed -->
- [ ] `CONTRIBUTING.md`: <!-- What changed -->
- [ ] Code comments/XML docs: <!-- Coverage -->
- [ ] API documentation (Swagger): <!-- What changed -->
- [ ] Integration guides: <!-- What changed -->

### Documentation Verification
- [ ] All public APIs have XML documentation
- [ ] README accurately reflects current functionality
- [ ] Integration examples work as documented
- [ ] Migration guides provided for breaking changes

---

## Deployment Instructions

### Pre-Deployment Steps
1. <!-- Step 1 -->
2. <!-- Step 2 -->

### Deployment Steps
1. <!-- Step 1 -->
2. <!-- Step 2 -->

### Post-Deployment Verification
1. <!-- Verification step 1 -->
2. <!-- Verification step 2 -->

### Rollback Plan
<!-- How to rollback if deployment fails -->
1. <!-- Rollback step 1 -->
2. <!-- Rollback step 2 -->

---

## Reviewer Checklist

<!-- For reviewers to check off during review -->

### Code Quality
- [ ] Code follows project conventions and style guide
- [ ] No code smells or anti-patterns
- [ ] Proper error handling throughout
- [ ] No performance regressions
- [ ] No memory leaks or resource leaks

### Testing
- [ ] All new code is covered by tests
- [ ] Tests are clear and maintainable
- [ ] Edge cases are covered
- [ ] No flaky tests introduced
- [ ] Tests pass consistently

### Documentation
- [ ] All acceptance criteria addressed
- [ ] Business value clearly articulated
- [ ] Risks identified and mitigated
- [ ] API changes documented
- [ ] Code is self-documenting or well-commented

### Security
- [ ] Security scan passed
- [ ] No new vulnerabilities introduced
- [ ] Authentication/authorization correct
- [ ] Input validation comprehensive

---

## Additional Notes

<!-- Any additional context, screenshots, performance benchmarks, etc. -->

### Performance Impact
<!-- Benchmarks if applicable -->

### Screenshots/Videos
<!-- For UI changes -->

### Related PRs
<!-- Link to related PRs in other repositories -->

---

## Product Owner Review Requirements

<!-- Checklist per .github/copilot-instructions.md -->

- [ ] ✅ CI repeatability evidence provided (3+ successful runs)
- [ ] ✅ Explicit AC traceability matrix included
- [ ] ✅ Failure semantics documented (timeout/retry strategies)
- [ ] ✅ Negative-path integration tests included
- [ ] ✅ Verification commands with expected outputs provided
- [ ] ✅ Business value quantified with specific metrics
- [ ] ✅ Risk assessment includes measurable risk reduction
- [ ] ✅ Roadmap alignment documented

---

**PR Author**: <!-- Your GitHub username -->  
**Date Created**: <!-- YYYY-MM-DD -->  
**Target Release**: <!-- e.g., "MVP v1.0" -->

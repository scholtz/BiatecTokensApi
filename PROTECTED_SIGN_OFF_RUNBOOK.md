# Protected Sign-Off Environment — Operational Runbook

## Purpose

This runbook describes the backend protected sign-off environment for the Biatec Tokens API.
A "protected sign-off" is a deterministic, fail-closed backend run that proves the core product
promise: non-crypto-native enterprises can use a secure backend-mediated token issuance flow
without managing wallets directly.

The environment supports:

- **Real email/password authentication** via JWT + ARC76 address derivation
- **Deterministic deployment initiation** and lifecycle tracking
- **Explicit terminal states** with evidence fields (`assetId`, `deploymentId`, proof documents)
- **Fail-closed configuration guards** — missing required secrets cause `Misconfigured` status

A passing protected run is the primary MVP sign-off artifact. It turns the narrative from
"the gate exists but the environment is not configured" into "the critical backend issuance
journey is operationally proven."

---

## Required Secrets and Configuration Keys

The following configuration keys **must** be present and non-empty for the backend to accept
a protected sign-off run. Missing keys cause `Misconfigured` environment status; the run cannot
proceed until they are set.

| Key | Purpose | Example |
|-----|---------|---------|
| `JwtConfig:SecretKey` | JWT token signing key (≥ 32 characters) | Set via user secrets or env var |
| `App:Account` | Algorand mnemonic for wallet-less ARC76 derivation | 25-word mnemonic phrase |

> **Never commit secrets to source control.** Use ASP.NET User Secrets for local development
> or environment variables for CI and production deployments.

### GitHub Actions Secrets (Protected CI Workflow)

The `protected-sign-off.yml` workflow requires these GitHub secrets.  Secrets **must** be
set in the `protected-sign-off` GitHub environment so that:

1. Environment-scoped secrets are accessible within the environment boundary.
2. Required-reviewer approval gates can be enforced before any secret is consumed.
3. The workflow's prerequisite check runs after reviewer approval, not in a separate
   pre-environment job that cannot see environment-scoped secrets.

| GitHub Secret | Maps To | Purpose |
|---|---|---|
| `PROTECTED_SIGN_OFF_JWT_SECRET` | `JwtConfig__SecretKey` | JWT signing key (≥ 32 characters) |
| `PROTECTED_SIGN_OFF_APP_ACCOUNT` | `App__Account` | Algorand account mnemonic (25 words) |

#### How to add secrets in GitHub

1. Navigate to **Settings → Environments** and create/select the `protected-sign-off` environment.
2. Under **Environment secrets**, add `PROTECTED_SIGN_OFF_JWT_SECRET` and `PROTECTED_SIGN_OFF_APP_ACCOUNT`.
3. Optionally configure **Required reviewers** so the business owner must approve each run.

> Repository-level secrets also work (no environment required), but environment secrets
> are preferred so required-reviewer gating applies to every protected run.

### Setting Secrets Locally

```bash
# JWT secret key
dotnet user-secrets set "JwtConfig:SecretKey" "your-secret-key-minimum-32-chars"

# Algorand account mnemonic
dotnet user-secrets set "App:Account" "word1 word2 word3 ... word25"
```

### Setting Secrets in CI / Docker

Use environment variables with the `__` separator for nested keys:

```bash
export JwtConfig__SecretKey="your-secret-key"
export App__Account="your-mnemonic"
```

### Optional Protected Sign-Off Configuration

The `ProtectedSignOff` configuration section is optional. When absent, defaults are used.

```json
{
  "ProtectedSignOff": {
    "SignOffIssuerId": "biatec-protected-sign-off-issuer",
    "SignOffAdminUserId": "biatec-sign-off-admin@biatec.io",
    "EnforceConfigGuards": true,
    "EnvironmentLabel": "staging"
  }
}
```

| Field | Default | Description |
|-------|---------|-------------|
| `SignOffIssuerId` | `biatec-protected-sign-off-issuer` | Protected tenant issuer ID |
| `SignOffAdminUserId` | `biatec-sign-off-admin@biatec.io` | Admin identity for the protected tenant |
| `EnforceConfigGuards` | `true` | Validate required keys; set `false` only in isolated unit tests |
| `EnvironmentLabel` | `default` | Label for diagnostics logs (e.g., "staging", "release-candidate") |

---

## Protected Sign-Off Endpoints

All four endpoints are authenticated (`[Authorize]`). Include a valid JWT bearer token in the
`Authorization: Bearer <token>` header.

### 1. Environment Check

**`POST /api/v1/protected-sign-off/environment/check`**

Checks whether the backend is ready for a protected sign-off run. Returns a structured
readiness report with per-component checks and actionable guidance.

```json
{
  "correlationId": "my-run-001",
  "includeConfigCheck": true,
  "includeFixtureCheck": true,
  "includeObservabilityCheck": true
}
```

**Expected happy-path response:**

```json
{
  "status": "Ready",
  "isReadyForProtectedRun": true,
  "criticalFailCount": 0,
  "checks": [
    { "name": "RequiredConfigurationPresent", "category": "Configuration", "outcome": "Pass" },
    { "name": "AuthServiceAvailable", "category": "Authentication", "outcome": "Pass" },
    ...
  ]
}
```

**When `status` is `Misconfigured`**, inspect the `checks` array for items with
`category: "Configuration"` and `outcome: "CriticalFail"`. Follow `operatorGuidance`
to resolve missing secrets.

### 2. Lifecycle Verification

**`POST /api/v1/protected-sign-off/lifecycle/execute`**

Runs a deterministic stage-by-stage verification of the full enterprise sign-off lifecycle:
Authentication → Initiation → StatusPolling → TerminalState → Validation → Complete.

```json
{
  "correlationId": "my-run-001",
  "issuerId": "biatec-protected-sign-off-issuer",
  "deploymentId": "sign-off-fixture-deployment-001"
}
```

**Expected happy-path response:**

```json
{
  "isLifecycleVerified": true,
  "reachedStage": "Complete",
  "failedStageCount": 0,
  "stages": [
    { "stage": "Authentication", "outcome": "Verified" },
    { "stage": "Initiation",     "outcome": "Verified" },
    { "stage": "StatusPolling",  "outcome": "Verified" },
    { "stage": "TerminalState",  "outcome": "Verified" },
    { "stage": "Validation",     "outcome": "Verified" },
    { "stage": "Complete",       "outcome": "Verified" }
  ]
}
```

### 3. Fixture Provisioning

**`POST /api/v1/protected-sign-off/fixtures/provision`**

Seeds the default protected tenant (issuer + admin user). Idempotent by default — safe to
call multiple times.

```json
{
  "correlationId": "my-run-001",
  "resetIfExists": false
}
```

**Call this before the lifecycle verification** to ensure the protected tenant fixtures are
provisioned.

### 4. Diagnostics

**`GET /api/v1/protected-sign-off/diagnostics?correlationId=my-run-001`**

Returns a structured diagnostics report distinguishing:

- **Configuration failures** — missing required secrets
- **Service availability failures** — unregistered DI services
- **Authorization failures** — missing role or permissions
- **Contract failures** — unexpected API response shapes
- **Lifecycle failures** — invalid state transitions

Does **not** expose secret values. Safe to include in operator runbooks and CI logs.

---

## Protected Run — Canonical Happy Path

Run these steps in order to prove the backend protected sign-off path:

### Step 1: Verify Environment Readiness

```bash
curl -X POST https://<backend>/api/v1/protected-sign-off/environment/check \
  -H "Authorization: Bearer <jwt-token>" \
  -H "Content-Type: application/json" \
  -d '{"correlationId":"release-run-001","includeConfigCheck":true,"includeFixtureCheck":true}'
```

**Pass criteria:** `status: "Ready"` and `isReadyForProtectedRun: true`.

If `status: "Misconfigured"`, set the missing secrets (see section above) and retry.
If `status: "Unavailable"`, verify DI service registrations in `Program.cs`.
If `status: "Degraded"`, optional features are absent but a run can still proceed.

### Step 2: Provision Enterprise Fixtures

```bash
curl -X POST https://<backend>/api/v1/protected-sign-off/fixtures/provision \
  -H "Authorization: Bearer <jwt-token>" \
  -H "Content-Type: application/json" \
  -d '{"correlationId":"release-run-001"}'
```

**Pass criteria:** `isProvisioned: true`.

### Step 3: Execute Lifecycle Verification

```bash
curl -X POST https://<backend>/api/v1/protected-sign-off/lifecycle/execute \
  -H "Authorization: Bearer <jwt-token>" \
  -H "Content-Type: application/json" \
  -d '{"correlationId":"release-run-001"}'
```

**Pass criteria:** `isLifecycleVerified: true` and `reachedStage: "Complete"`.

### Step 4: Retrieve Diagnostics (optional)

```bash
curl https://<backend>/api/v1/protected-sign-off/diagnostics?correlationId=release-run-001 \
  -H "Authorization: Bearer <jwt-token>"
```

**Pass criteria:** `isOperational: true` and `failureCategories.hasConfigurationFailure: false`.

---

## Permissive CI vs Release-Grade Evidence

**This distinction is critical for product-owner sign-off and enterprise procurement reviews.**

Permissive runs use test-only in-memory secrets and do not exercise live backend paths.
Release-grade runs use validated production-strength secrets and prove the full deployment
lifecycle under the protected environment.  Confusing the two can lead to false confidence
during enterprise procurement or pre-release sign-off reviews.

### Permissive developer-feedback lanes

The following pipeline runs are **NOT** release-grade evidence and **MUST NOT** be used
as product-owner sign-off proof:

| Lane | Trigger | Evidence grade | Why it is not release-grade |
|---|---|---|---|
| Tier 1 `build-and-test` job | Push to master/main or PR | ❌ Permissive | Uses test-only in-memory keys; no real secrets; no live backend calls |
| Tier 1 on PR from copilot/dependabot | Pull request | ❌ Permissive | Same as above; additionally runs with read-only permissions |
| Any local `dotnet test` run | Developer workstation | ❌ Permissive | Uses factory-supplied JWT secrets; no protected environment |
| Tier 2 `dry_run=true` dispatch | workflow_dispatch | ❌ Dry run | Validates secrets and builds only; no endpoint calls |

All permissive runs produce a Tier 1 GitHub Actions step summary containing:

> ⚠️ **This is a permissive developer-feedback run — NOT release-grade evidence.**

This notice is machine-verifiable: CI30 regression test asserts it is present.

### What makes a run release-grade

A run is **release-grade evidence** (`isReleaseGradeEvidence: true` in the manifest) only when
**all** of the following conditions are satisfied simultaneously:

| Condition | How verified |
|---|---|
| Triggered via `workflow_dispatch` (Tier 2) | Workflow `if: github.event_name == 'workflow_dispatch'` |
| Runs under `environment: protected-sign-off` | GitHub environment boundary enforces secret scoping |
| `PROTECTED_SIGN_OFF_JWT_SECRET` is present and ≥ 32 characters | Step 0 sanity check (fails workflow if violated) |
| `PROTECTED_SIGN_OFF_APP_ACCOUNT` is present and exactly 25 words | Step 0 sanity check (fails workflow if violated) |
| `isReadyForProtectedRun: true` from live environment check endpoint | Step D evidence collection |
| `isLifecycleVerified: true` and `reachedStage: "Complete"` from live lifecycle endpoint | Step D + Step I release gate enforcement |

The evidence manifest (`00_evidence_manifest.json`) contains `"isReleaseGradeEvidence": true`
only when both `lifecycleVerified` and `isReadyForProtectedRun` are true.  A manifest with
`"isReleaseGradeEvidence": false` **does not qualify** for product-owner sign-off regardless
of other fields.

### How product owners distinguish the two

1. **Tier 1 (permissive):** GitHub Actions step summary shows the ⚠️ permissive-lane notice.
   No evidence artifact is produced.

2. **Tier 2 (release-grade):** Evidence artifact `protected-sign-off-evidence-<corr-id>` is
   produced and retained for 90 days.  Open `00_evidence_manifest.json` and confirm:
   - `"isReleaseGradeEvidence": true`
   - `"lifecycleVerified": true`
   - `"reachedStage": "Complete"`

   If any of these values is missing or false, the run is not acceptable release evidence.

### Negative-path behavior

The strict gate is fail-closed: missing secrets, malformed secrets, unreachable backend, or
failed lifecycle verification all cause the Tier 2 job to fail.  A passing Tier 2 job with a
`isReleaseGradeEvidence: false` manifest **cannot** occur because Step I enforces the lifecycle
gate before the job can succeed.

---

## What the Protected Run Proves

A successful protected run produces evidence that:

1. **Authentication** — JWT + ARC76 infrastructure is wired and operational
2. **Initiation** — The issuer workflow service can accept deployment initiation requests
3. **Status Polling** — Deployment status returns deterministic, contract-stable responses
4. **Terminal State** — The lifecycle service returns structured terminal-state responses
5. **Validation** — The sign-off service generates structured proof documents with `ProofId` and `DeploymentId`

The evidence supports:
- MVP release governance decisions
- Procurement discussions requiring backend issuance proof
- Internal CI confidence that the core product path is operational

---

## Failure Modes and Remediation

| Failure Mode | Status | Remediation |
|---|---|---|
| Missing `PROTECTED_SIGN_OFF_JWT_SECRET` in GitHub environment | Workflow fails (Step 0) | Add secret to `protected-sign-off` GitHub environment |
| `PROTECTED_SIGN_OFF_JWT_SECRET` is < 32 characters | Workflow fails (Step 0 sanity check) | Replace with a ≥ 32-character secret |
| Missing `PROTECTED_SIGN_OFF_APP_ACCOUNT` in GitHub environment | Workflow fails (Step 0) | Add 25-word Algorand mnemonic to `protected-sign-off` environment |
| `PROTECTED_SIGN_OFF_APP_ACCOUNT` has wrong word count | Workflow fails (Step 0 sanity check) | Replace with a valid 25-word Algorand mnemonic |
| Missing `JwtConfig:SecretKey` | `Misconfigured` | Set via user secrets or env var |
| Missing `App:Account` mnemonic | `Misconfigured` | Set via user secrets or env var |
| `WorkflowGovernanceConfig:Enabled` = false | `Degraded` | Set to `true` (or remove key for default) |
| DI service not registered | `Unavailable` | Check `Program.cs` service registrations |
| No active admin member in issuer | `Degraded` | Call `POST /fixtures/provision` |
| State machine returns invalid | `Degraded` | Inspect IssuerWorkflowService implementation |
| Authentication stage fails | `Lifecycle.Failed` | Verify JWT config and ARC76 settings |
| Initiation stage fails | `Lifecycle.Failed` | Verify issuer fixtures and state machine |
| Status polling throws | `Lifecycle.Failed` | Verify IBackendDeploymentLifecycleContractService |
| Validation throws | `Lifecycle.Failed` | Verify IDeploymentSignOffService configuration |
| `isReleaseGradeEvidence: false` in manifest | Not release-grade | Rerun after resolving all lifecycle and environment failures |

---

## Required Release Gate Configuration

This section documents how to configure the protected strict sign-off workflow as a **required
status check** so it becomes an unmissable, auditable gate before business-owner release approval.

### Why this matters

Without branch-protection enforcement, the protected sign-off check is visible but optional.
Anyone merging to `master`/`main` could proceed even if the check fails. Configuring it as
a required status check makes it fail-closed at the repository level — no merge proceeds
until the protected strict sign-off tests pass.

### Step 1 — Enable the required status check in branch protection

1. Navigate to **Settings → Branches** in the GitHub repository.
2. Click **Edit** next to the `master` (or `main`) branch protection rule, or create one if
   none exists.
3. Enable **Require status checks to pass before merging**.
4. In the **Search for status checks** box, type **`Build and run protected sign-off tests`**.
   This is the name of the Tier 1 job in `protected-sign-off.yml`.
5. Select the check from the dropdown and click **Save changes**.

> **Note**: The status check name must match exactly.  The check is produced by the
> `build-and-test` job in `.github/workflows/protected-sign-off.yml` (job name: `Build and run
> protected sign-off tests`).  If the check does not appear in the search box, trigger a PR
> against `master`/`main` to create the first run that registers the status check name.

### Step 2 — Configure the protected-sign-off environment for Tier 2 runs

For the full evidence collection (Tier 2), the `protected-sign-off` GitHub environment must be
configured with the required secrets and, optionally, required reviewers:

1. Navigate to **Settings → Environments** and create or edit the `protected-sign-off` environment.
2. Under **Environment secrets**, add `PROTECTED_SIGN_OFF_JWT_SECRET` and
   `PROTECTED_SIGN_OFF_APP_ACCOUNT`.
3. Optionally add **Required reviewers** (e.g., the business owner or release manager) so every
   Tier 2 run requires explicit approval.

### Step 3 — Release approval checklist for product owners

Before approving a release, the product owner should verify:

| Step | Where to check | Pass criterion |
|---|---|---|
| Protected sign-off tests pass | PR status checks | ✅ `Build and run protected sign-off tests` green |
| Full evidence run completed | Actions → Protected Strict Sign-Off → Latest dispatch | ✅ Workflow completed successfully |
| Evidence artifact present | Actions → workflow run → Artifacts | ✅ `protected-sign-off-evidence-<corr-id>` downloadable |
| **isReleaseGradeEvidence** | `00_evidence_manifest.json` in artifact | **`"isReleaseGradeEvidence": true`** — this is the primary sign-off criterion |
| Lifecycle verified | `00_evidence_manifest.json` | `"lifecycleVerified": true`, `"reachedStage": "Complete"` |
| Governance check passed | `00_evidence_manifest.json` | `"observedGovernanceCheckOutcome": "Pass"` |
| Environment ready | `01_environment_check.json` in artifact | `"status": "Ready"`, `"isReadyForProtectedRun": true` |

> ⚠️ **If `isReleaseGradeEvidence` is `false` or absent, the run is NOT acceptable evidence**
> regardless of what other fields show.  Do not approve the release until a run with
> `"isReleaseGradeEvidence": true` is available.

If any other step shows a failure or non-passing value, **do not approve the release** until the
failure is remediated.  The runbook's Failure Modes section documents remediations for each
failure type.

### Status check naming

The following table maps each status check to its canonical job:

| Status check name | Workflow file | Job | When it runs |
|---|---|---|---|
| `Build and run protected sign-off tests` | `protected-sign-off.yml` | `build-and-test` | Every PR and push to master/main |
| `Protected strict sign-off run` | `protected-sign-off.yml` | `protected-sign-off-run` | `workflow_dispatch` only |
| `PR Test Results` | `test-pr.yml` | `pr-tests` | Every PR |

The `Build and run protected sign-off tests` check is the one that must be configured as a
required status check.  The `Protected strict sign-off run` check is the full evidence run and
is triggered manually — it is not suitable as an automated required check but is reviewed as
part of the release approval checklist.

---

## Protected Sign-Off CI Workflow

The `.github/workflows/protected-sign-off.yml` workflow is the primary release gate for
business-owner MVP sign-off. It runs automatically on push to `main`/`master`, on every PR
targeting `main`/`master`, and can be triggered manually via `workflow_dispatch`.

### Two-tier design with PR surfacing

The Tier 1 `build-and-test` job runs on:
- Every **push** to `master`/`main` — confirms that merges keep the sign-off suite green
- Every **pull request** targeting `master`/`main` — surfaces the check for branch-protection enforcement

This means the check name `Build and run protected sign-off tests` will appear in the PR status
checks UI and can be added as a required branch-protection rule (see Required Release Gate
Configuration above).

### Single-job design (environment-boundary prerequisite check)

The workflow uses a **single job** with `environment: protected-sign-off`.  This is intentional:

- A separate "validate-prerequisites" job running without the environment attached cannot see
  environment-level secrets.  Storing secrets in the environment (the recommended setup for
  required-reviewer gating) would cause a pre-environment secrets check to fail silently.
- With a single job, the reviewer approval gate fires before any step executes.
- The first step inside the job validates secrets (fail-closed), with the environment context
  already active so both repository-level and environment-level secrets are resolvable.

### Workflow steps

1. **Step 0 — Validate prerequisites (presence + sanity)**: Checks both required secrets for
   presence and sanity.  Fails with actionable diagnostics if either is absent **or** malformed:
   - `PROTECTED_SIGN_OFF_JWT_SECRET` must be non-empty **and** ≥ 32 characters.
   - `PROTECTED_SIGN_OFF_APP_ACCOUNT` must be non-empty **and** exactly 25 words.
   Both presence and sanity failures are reported separately in the GitHub Actions step summary
   so operators can distinguish "secret not set" from "secret has wrong format."
   This is the first step, so the job fails early without reaching any expensive operation.
2. **Step A — Tests**: Runs all ProtectedSignOff tests with `WebApplicationFactory`-hosted servers.
   `JwtConfig__SecretKey` is **not** injected — integration tests use their own in-memory JWT
   configuration to avoid a key-mismatch between the startup snapshot and the live `IOptions` binding.
   `App__Account` and `KeyManagementConfig__*` are provided for Algorand address derivation.
   Governance evidence comes from runtime config, not injected values.
3. **Step B — Backend**: Starts the backend in-process with the real JWT secret and mnemonic.
   No `WorkflowGovernanceConfig__*` overrides are injected; governance evidence reflects the
   application's actual runtime configuration.
4. **Steps C–D — Evidence**: Registers a user, obtains a JWT, calls all four protected sign-off
   endpoints.  Extracts the observed `WorkflowGovernanceEnabled` check outcome directly from the
   environment check API response.
5. **Steps E–H — Artifacts and summary**: Saves evidence JSON (90-day retention), sanitizes and
   publishes test results, produces a product-owner summary showing observed (not asserted)
   governance status.
6. **Step I — Release gate enforcement**: Exits non-zero if `lifecycleVerified` is not `true`.

### Evidence manifest — observed governance, not asserted

The `00_evidence_manifest.json` artifact contains:
```json
{
  "schemaVersion": "1.0",
  "correlationId": "protected-run-<run-id>-<attempt>",
  "runId": "<github-run-id>",
  "sha": "<commit-sha>",
  "ref": "refs/heads/main",
  "actor": "<github-actor>",
  "runAt": "<iso8601-timestamp>",
  "environmentLabel": "protected-ci",
  "isReleaseGradeEvidence": true,
  "releaseGradeNote": "true only when lifecycleVerified=true and isReadyForProtectedRun=true under the protected-sign-off environment with validated real secrets",
  "observedGovernanceCheckOutcome": "<Pass|DegradedFail|NotChecked>",
  "results": {
    "environmentStatus": "Ready",
    "isReadyForProtectedRun": true,
    "fixturesProvisioned": true,
    "lifecycleVerified": true,
    "reachedStage": "Complete",
    "diagnosticsOperational": true
  }
}
```

**Key fields for product-owner review:**
- `isReleaseGradeEvidence` — **the primary sign-off criterion**; `true` only when lifecycle and
  environment checks both passed under the protected environment with real validated secrets.
- `observedGovernanceCheckOutcome` — extracted from the live environment check API response,
  never hardcoded.  The workflow cannot inflate this to claim governance is passing when it is not.
- `lifecycleVerified` + `reachedStage` — confirm the full issuance lifecycle ran to completion.

When `isReleaseGradeEvidence` is `false`, the run **does not qualify** as product-owner sign-off
evidence, even if individual checks appear to have passed.

### Triggering a manual run

1. Navigate to **Actions → Protected Strict Sign-Off**.
2. Click **Run workflow**.
3. Optionally supply a correlation ID (defaults to `protected-run-<run-id>-<attempt>`).
4. Optionally enable `dry_run` to validate prerequisites and build only.
5. If the `protected-sign-off` environment requires reviewers, approve the run when prompted.

### Artifacts produced

Each successful run saves two artifacts retained for 90 days:

| Artifact | Contents |
|---|---|
| `protected-sign-off-evidence-<corr-id>` | JSON files: evidence manifest, environment check, fixture provision, lifecycle result, diagnostics |
| `protected-sign-off-test-results-<corr-id>` | TRX test results for all ProtectedSignOff unit and integration tests |

The evidence manifest (`00_evidence_manifest.json`) contains:
- `correlationId`, `runId`, `sha`, `ref`, `actor`, `runAt`
- `isReleaseGradeEvidence` — **primary sign-off criterion** (`true` only when lifecycle + environment both verified under protected secrets)
- `observedGovernanceCheckOutcome` — extracted from the runtime API response, not hardcoded
- Per-check results: `environmentStatus`, `isReadyForProtectedRun`, `fixturesProvisioned`, `lifecycleVerified`, `reachedStage`, `diagnosticsOperational`

### Release gate enforcement

The workflow enforces a hard pass requirement on `lifecycleVerified == true`. If lifecycle
verification fails, the workflow exits non-zero, the run is marked failed, and no release
gate credit is awarded. This is intentionally fail-closed.

### Governance check

The environment check validates `WorkflowGovernanceConfig:Enabled`. When governance is
disabled the check returns `DegradedFail` and the environment status becomes `Degraded`,
signalling that the run does not qualify as a governance-backed release gate. The default
(`Enabled: true`) always passes.

The workflow never injects `WorkflowGovernanceConfig__Enabled=true` as an environment variable.
Governance evidence is therefore an observation of the real environment, not a workflow assertion.

---

## What Is Intentionally Out of Scope

This environment does **not** cover:

- Live Algorand mainnet or testnet blockchain transactions
- Real Stripe subscription or KYC verification
- Multi-chain EVM token deployment
- Production customer data or real user credentials
- Regulator-grade audit export (covered by `EnterpriseComplianceReviewService`)

These are separate product capabilities with their own sign-off paths.

---

## Regression Protection

The six test classes lock in this contract (161 tests total):

- **`ProtectedSignOffEnvironmentTests`** (54 tests) — unit + integration tests for all four HTTP
  endpoints, configuration guards, and lifecycle stages
- **`ProtectedSignOffWorkflowGovernanceTests`** (16 tests) — unit tests for the
  `WorkflowGovernanceEnabled` check across enabled/disabled/absent configurations
- **`ProtectedSignOffEvidenceIntegrityTests`** (12 tests) — integration tests (HTTP via
  `WebApplicationFactory`) proving that governance status and readiness are observed from
  actual runtime configuration, not from workflow-injected values
- **`ProtectedSignOffLifecycleContractTests`** (29 tests) — per-stage contract tests (LC1–LC30)
  asserting the exact field values, ordering, count semantics, and failure-chain behaviour
  expected by the strict Playwright suite and evidence manifest
- **`ProtectedSignOffCIWorkflowConfigTests`** (29 tests) — CI configuration regression-prevention
  tests (CI1–CI29) documenting the exact env var set safe for `dotnet test`, the JWT key
  constraint, authentication failure paths, fail-closed configuration guards, workflow YAML
  validity (CI23 verifies no column-0 Python), required release gate configuration
  (CI24–CI28 validate PR trigger surfacing, release gate enforcement step, evidence manifest
  schema contract, and artifact retention), and CI29 which validates `continue-on-error: true`
  is set on the publish step to prevent 403 errors from copilot/dependabot PRs cascading into
  workflow failures
- **`ProtectedSignOffEndToEndTests`** (21 tests) — canonical in-process protected strict sign-off
  run evidence (E2E01–E2E20); exercises the full journey via HTTP through `WebApplicationFactory`
  (register → JWT → fixture provision → environment check → lifecycle execute → diagnostics →
  evidence manifest construction); proves determinism across 3 independent runs and includes
  negative-path tests for authentication failure, missing-config fail-closed behavior, and
  attribution of stage failures when prerequisites are absent; E2E16–E2E20 add fixture reset
  consistency, fixture-check surfacing, stage outcome non-nullness, diagnostics category
  distinction, and the release gate predicate assertion

Run before every PR merge:

```bash
dotnet test BiatecTokensTests --filter "FullyQualifiedName~ProtectedSignOff" --configuration Release
```

Expected output: `Passed! - Failed: 0, Passed: 161`

### Critical: environment variables that must NOT be set for `dotnet test`

Two environment variables must **not** be passed to `dotnet test` for the ProtectedSignOff suite,
even though they look like valid CI configuration:

| Variable | Why it must NOT be set for `dotnet test` |
|---|---|
| `JwtConfig__SecretKey` | The integration tests use `WebApplicationFactory` with their own in-memory JWT configuration. Setting this env var causes a key-mismatch: the JWT bearer validation in `Program.cs` reads the env var value as a startup snapshot, while `IOptions<JwtConfig>` (used by `AuthenticationService` to sign tokens) reads the factory's in-memory config at runtime. The two values diverge, producing 401 Unauthorized on every authenticated call. |
| `ProtectedSignOff__EnforceConfigGuards` set to `false` | This suppresses `CheckWorkflowGovernanceConfig()`, removing the `WorkflowGovernanceEnabled` check from the environment check response. The Evidence Integrity (EI) integration tests assert that this check is always present, so they fail with "Expected: not null, But was: null". |

**Safe env vars for `dotnet test`** (these do not conflict with factory-managed configuration):

```bash
# Algorand account for ARC76 address derivation — factories accept any valid mnemonic
App__Account="abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about"
KeyManagementConfig__Provider="Hardcoded"
KeyManagementConfig__HardcodedKey="<≥32 chars>"
ProtectedSignOff__EnvironmentLabel="ci-push"  # informational label only
```

The JWT secret for the backend server (Steps B–D of the protected run) is correctly set via
`JwtConfig__SecretKey` for the `dotnet run` invocation only, not for `dotnet test`.

---

## Canonical In-Process Protected Sign-Off Evidence

The `ProtectedSignOffEndToEndTests` test class (E2E01–E2E15) is the canonical in-process
proof that the backend-managed issuance critical path works in a fail-closed manner.  These
tests exercise the complete protected sign-off journey via HTTP through `WebApplicationFactory`
and can be run locally without any external service dependencies.

### What these tests prove

| Test | Stage | Evidence |
|------|-------|----------|
| E2E01 | Full journey | Builds a structured evidence manifest with all required fields |
| E2E02 | Lifecycle gate | `IsLifecycleVerified=true`, `ReachedStage=Complete` |
| E2E03 | Environment | `Status=Ready`, `IsReadyForProtectedRun=true` (after fixture provision) |
| E2E03b | Config-only | Config check alone returns Ready without fixture prerequisites |
| E2E04 | Fixtures | `IsProvisioned=true`, issuer ID returned |
| E2E05 | Diagnostics | `IsOperational=true`, no configuration failures |
| E2E06 | Evidence artifact | Full manifest is JSON-serializable with all fields populated |
| E2E07 | Determinism | Identical results across 3 independent factory instances |
| E2E08 | Auth failure | Wrong-password returns 401, distinguishable from backend unavailability |
| E2E09 | Stage attribution | Failed lifecycle attributes failure to specific stage with guidance |
| E2E10 | Fail-closed | Missing required config → `Misconfigured` status (not `Ready`) |
| E2E11 | Stage ordering | All 6 stages present in enum order with Verified outcomes |
| E2E12 | Evidence quality | All Verified stages have null `UserGuidance` (clean artifact) |
| E2E13 | Tracing | CorrelationId echoed across all 4 API endpoints |
| E2E14 | Governance | `WorkflowGovernanceEnabled` check is observable in response |
| E2E15 | Concurrency | Two parallel users each complete the full journey independently |

### Running the in-process protected sign-off evidence

```bash
# Full in-process protected run (all 16 E2E tests: E2E01–E2E15 + variant E2E03b)
dotnet test BiatecTokensTests --filter "FullyQualifiedName~ProtectedSignOffEndToEndTests" \
  --configuration Release

# Expected output
# Passed!  - Failed: 0, Passed: 16, Total: 16
```

### How this maps to the Tier 2 CI workflow

| Tier 2 CI step | Corresponding E2E test | Evidence field |
|---|---|---|
| Step B: Environment check | E2E03, E2E03b, E2E14 | `isReadyForProtectedRun`, governance outcome |
| Step C: Fixture provision | E2E04 | `isProvisioned`, `issuerId` |
| Step D: Lifecycle execute | E2E02, E2E11, E2E12 | `isLifecycleVerified`, `reachedStage` |
| Step E: Diagnostics | E2E05 | `isOperational`, failure categories |
| Step F: Evidence manifest | E2E01, E2E06 | Full manifest JSON with all fields |
| Step G: Determinism | E2E07 | 3 identical runs |
| Negative: Auth failure | E2E08 | 401 vs 503 distinction |
| Negative: Fail-closed | E2E10 | Misconfigured status on missing config |
| Negative: Stage attribution | E2E09 | `actionableGuidance` with stage name |

---

## Two-Tier CI Design

The `protected-sign-off.yml` workflow uses a two-tier design:

### Tier 1 — `build-and-test` job (push to master/main)

Runs automatically on every merge. Does **not** require the `protected-sign-off` GitHub environment
or any secrets. Builds the solution and runs all 150 ProtectedSignOff tests. Never fails due to
missing secrets — only fails on build errors or test regressions.

### Tier 2 — `protected-sign-off-run` job (workflow_dispatch only)

Runs only when manually triggered via `workflow_dispatch`. Requires:

1. The `protected-sign-off` GitHub environment to exist (Settings → Environments).
2. Both required secrets set in that environment.
3. Optional: required-reviewer approval if the environment is configured with reviewers.

This job performs the full evidence collection against a live backend instance and produces an
artifact-backed evidence manifest for product-owner sign-off.

---

## Related Resources

- Business roadmap: https://github.com/scholtz/biatec-tokens/blob/main/business-owner-roadmap.md
- Protected sign-off workflow: `.github/workflows/protected-sign-off.yml`
- Deployment sign-off service: `DeploymentSignOffService` / `DeploymentSignOffController`
- Backend lifecycle contract: `BackendDeploymentLifecycleContractService`
- Issuer workflow service: `IssuerWorkflowService`
- Enterprise compliance review: `EnterpriseComplianceReviewService`

---

## Optional: External Live-Backend Mode

The default Tier 2 job starts an in-process backend using the `PROTECTED_SIGN_OFF_JWT_SECRET` and
`PROTECTED_SIGN_OFF_APP_ACCOUNT` secrets.  A future extension may support pointing the workflow at
a pre-deployed external backend instead.  If that mode is added, the following additional secrets
would be required in the `protected-sign-off` environment:

| GitHub Secret | Purpose |
|---|---|
| `SIGNOFF_API_BASE_URL` | Base URL of the live deployed backend (e.g. `https://api.biatec.io`) |
| `SIGNOFF_TEST_EMAIL` | Email address of a pre-registered operator account |
| `SIGNOFF_TEST_PASSWORD` | Password for the operator account |

When `SIGNOFF_API_BASE_URL` is set, the workflow would skip the in-process backend startup steps
and call the external endpoint directly.  `SIGNOFF_TEST_EMAIL` / `SIGNOFF_TEST_PASSWORD` would
replace the register-on-the-fly approach used by the current self-hosted path.

Until external-backend mode is implemented, these three secrets are **not** required.

---

## Workflow YAML Maintenance Rules

**CRITICAL: Never embed Python code with bare newlines at column 0 inside a YAML `run:` block.**

GitHub Actions workflows use YAML literal block scalars (`run: |`) whose indentation level is set
by the first non-empty content line (typically 10 spaces).  Any line at column 0 inside that block
is treated by the YAML parser as a new top-level mapping key, making the entire workflow file
invalid.  When a workflow file is invalid, GitHub marks the run as `failure` with zero jobs
started — a symptom that can be hard to diagnose without running the YAML through a validator.

**Safe patterns inside a `run: |` block:**

```yaml
# ✅  Single-line python3 -c — no bare newlines inside the quoted string;
#     || fallback assigns default on error without a line-continuation character
GOV=$(echo "$DATA" | python3 -c "import sys,json; d=json.load(sys.stdin); print(d.get('key',''))" 2>/dev/null) || GOV="Error"

# ✅  python3 heredoc — content is indented at ≥10 spaces in YAML; GitHub strips
#     common indentation before passing to bash, so Python sees column-0 code
python3 - <<'PYEOF'
import sys, json
# ... rest of script
PYEOF

# ❌  INVALID — python3 -c with bare newline at column 0 breaks YAML parsing
GOV=$(echo "$DATA" | python3 -c "
import sys, json        # ← This line at column 0 invalidates the YAML file
...
")
```

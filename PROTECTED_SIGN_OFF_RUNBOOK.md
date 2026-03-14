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

The `protected-sign-off.yml` workflow requires these GitHub secrets, set either in the
`protected-sign-off` GitHub environment or as repository-level secrets:

| GitHub Secret | Maps To | Purpose |
|---|---|---|
| `PROTECTED_SIGN_OFF_JWT_SECRET` | `JwtConfig__SecretKey` | JWT signing key (≥ 32 characters) |
| `PROTECTED_SIGN_OFF_APP_ACCOUNT` | `App__Account` | Algorand account mnemonic (25 words) |

#### How to add secrets in GitHub

1. Navigate to **Settings → Environments** and create/select the `protected-sign-off` environment.
2. Under **Environment secrets**, add `PROTECTED_SIGN_OFF_JWT_SECRET` and `PROTECTED_SIGN_OFF_APP_ACCOUNT`.
3. Optionally configure **Required reviewers** so the business owner must approve each run.

For repository-level secrets (no environment approval needed):
1. Navigate to **Settings → Secrets and variables → Actions**.
2. Add the secrets with the exact names above.

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
| Missing CI secrets | Workflow fails | Add secrets to `protected-sign-off` GitHub environment |

---

## Protected Sign-Off CI Workflow

The `.github/workflows/protected-sign-off.yml` workflow is the primary release gate for
business-owner MVP sign-off. It runs automatically on push to `main`/`master` and can be
triggered manually via `workflow_dispatch`.

### Workflow jobs

1. **`validate-prerequisites`** — Checks that both required secrets are present. Fails immediately
   with actionable guidance if any secret is missing. No secrets are logged.

2. **`protected-sign-off-run`** — Builds the solution, runs all ProtectedSignOff tests with real
   secrets, starts the backend in-process, calls all four protected sign-off endpoints, and saves
   evidence artifacts.

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
- `workflowGovernanceEnabled: true`
- Per-check results: `environmentStatus`, `isReadyForProtectedRun`, `fixturesProvisioned`, `lifecycleVerified`, `reachedStage`, `diagnosticsOperational`

### Release gate enforcement

The workflow enforces a hard pass requirement on `lifecycleVerified == true`. If lifecycle
verification fails, the workflow exits non-zero, the run is marked failed, and no release
gate credit is awarded. This is intentionally fail-closed.

### Governance check

The environment check now validates `WorkflowGovernanceConfig:Enabled`. When governance is
disabled the check returns `DegradedFail` and the environment status becomes `Degraded`,
signalling that the run does not qualify as a governance-backed release gate. The default
(`Enabled: true`) always passes.

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

The `ProtectedSignOffEnvironmentTests` test class (54 tests) and `ProtectedSignOffWorkflowGovernanceTests`
(16 tests) lock in this contract:

- **Unit tests** — verify each service method with mocked dependencies
- **Configuration guard tests** — verify `Misconfigured` status for missing required keys
- **Governance tests** — verify `WorkflowGovernanceEnabled` check across enabled/disabled/absent configurations
- **Integration tests** — exercise all four HTTP endpoints via `WebApplicationFactory`
- **Determinism tests** — verify identical outcomes across three consecutive runs

Run before every PR merge:

```bash
dotnet test BiatecTokensTests --filter "FullyQualifiedName~ProtectedSignOff" --configuration Release
```

Expected output: `Passed! - Failed: 0, Passed: 70`

---

## Related Resources

- Business roadmap: https://github.com/scholtz/biatec-tokens/blob/main/business-owner-roadmap.md
- Protected sign-off workflow: `.github/workflows/protected-sign-off.yml`
- Deployment sign-off service: `DeploymentSignOffService` / `DeploymentSignOffController`
- Backend lifecycle contract: `BackendDeploymentLifecycleContractService`
- Issuer workflow service: `IssuerWorkflowService`
- Enterprise compliance review: `EnterpriseComplianceReviewService`

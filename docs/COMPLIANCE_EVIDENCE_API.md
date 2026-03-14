# Compliance Evidence and Launch Decision API

**Route prefix:** `api/v1/compliance-evidence`

This document describes the backend compliance evidence and launch decision service, which provides enterprise-grade, regulator-ready compliance evidence for token launches on the Biatec Tokens platform.

---

## Overview

The Compliance Evidence API answers the core enterprise compliance questions before every token launch and during every audit cycle:

- **What evidence proves this release was sign-off-ready?**
- **Which compliance checks passed? Which policies were active?**
- **Can we export a package for auditors, legal counsel, or procurement reviewers?**
- **Is this bundle release-grade or permissive/incomplete?**

All evaluations are deterministic, idempotent, and produce structured decision records with full rule traces that are useful in compliance audits and regulator reviews.

---

## Endpoints

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `POST` | `/decision` | Required | Evaluate launch readiness and produce a compliance decision |
| `GET`  | `/decision/{id}` | Required | Retrieve a previously recorded decision |
| `GET`  | `/decision/{id}/trace` | Required | Retrieve the full rule evaluation trace |
| `GET`  | `/decisions/{ownerId}` | Required | List recent decisions for an owner |
| `POST` | `/evidence` | Required | Retrieve a compliance evidence bundle |
| `POST` | `/evidence/export/json` | Required | Download evidence bundle as JSON artifact |
| `POST` | `/evidence/export/csv` | Required | Download evidence bundle as CSV artifact |
| `GET`  | `/health` | Public | API health check |

---

## Authentication

All endpoints except `/health` require a valid Bearer JWT token (email/password authentication via `POST /api/v1/auth/login`).

```http
Authorization: Bearer <jwt-token>
```

---

## Release-Grade Evidence

The key differentiator between this API and a generic status check is the **release-grade classification**.

### What is release-grade evidence?

Evidence is **release-grade** (`isReleaseGradeEvidence: true`) only when ALL of the following conditions are met:

1. **No blockers** – No rules have produced a `Fail` outcome with severity `High` or `Critical`.
2. **Current policy version** – The evaluation was run against the current policy version (`2026.03.07.1` or later).
3. **All evidence is valid** – No evidence items have `Invalid` or `Stale` validation status.
4. **Decision status** – The decision status is `Ready` or `Warning` (not `Blocked` or `NeedsReview`).

### What makes evidence non-release-grade?

- Evidence older than 90 days (freshness = `Stale`)
- Any evidence item with `Invalid` or `Stale` validation status
- ARC1400 deployment without Premium subscription (entitlement blocker)
- Evaluation against a non-current policy version
- Decision status of `Blocked` or `NeedsReview`

### Why this matters

Never label incomplete or permissive data as release-grade. The `releaseGradeNote` field in every response explains the exact reasons, enabling compliance managers to understand what is missing and take corrective action.

---

## Evidence Model

### LaunchDecisionResponse

```json
{
  "decisionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "status": "Ready",
  "canLaunch": true,
  "summary": "All compliance prerequisites are met. Token launch is permitted.",
  "isReleaseGradeEvidence": true,
  "releaseGradeNote": "Release-grade: all mandatory checks passed on policy 2026.03.07.1, all evidence is valid, and no blockers are outstanding.",
  "blockers": [],
  "warnings": [
    {
      "warningId": "WARN-RULE-KYC-001",
      "title": "KYC/AML Status Advisory",
      "description": "KYC/AML verification is advisory for mainnet launches.",
      "severity": "Low",
      "category": "Identity",
      "suggestedActions": ["Complete issuer KYC/AML verification via the compliance portal."]
    }
  ],
  "recommendedActions": [],
  "evidenceSummary": [
    {
      "evidenceId": "abc123",
      "category": "Identity",
      "validationStatus": "Valid",
      "rationale": "Owner identity validated from request context",
      "collectedAt": "2026-03-14T21:00:00Z"
    }
  ],
  "policyVersion": "2026.03.07.1",
  "schemaVersion": "1.0.0",
  "correlationId": "corr-xyz",
  "decidedAt": "2026-03-14T21:00:00Z",
  "evaluationTimeMs": 12,
  "isIdempotentReplay": false,
  "isProvisional": false
}
```

### EvidenceBundleResponse

```json
{
  "bundleId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "ownerId": "user@example.com",
  "isReleaseGradeEvidence": true,
  "releaseGradeNote": "Release-grade: 9 evidence items are current and valid on policy 2026.03.07.1.",
  "freshnessStatus": "Fresh",
  "policySnapshot": {
    "policyVersion": "2026.03.07.1",
    "policyName": "Biatec Token Compliance Policy",
    "effectiveAt": "2026-03-07T00:00:00Z",
    "isCurrent": true,
    "scope": "Token issuance compliance for ASA, ARC3, ARC200, ERC20, ARC1400 standards"
  },
  "attestationRecords": [
    {
      "attestationId": "ATT-3fa85f64",
      "attestationType": "LAUNCH_DECISION_APPROVED",
      "issuedBy": "ComplianceEvidenceLaunchDecisionService",
      "issuedAt": "2026-03-14T21:00:00Z",
      "isValid": true,
      "description": "Launch decision 3fa85f64... passed all release-grade checks."
    }
  ],
  "auditTrailReferences": [
    {
      "auditId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "eventType": "LAUNCH_DECISION_EVALUATED",
      "occurredAt": "2026-03-14T21:00:00Z",
      "performedBy": "user@example.com",
      "description": "All compliance prerequisites are met. Token launch is permitted.",
      "category": "ComplianceDecision"
    }
  ],
  "exportManifest": {
    "exportId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "generatedAt": "2026-03-14T21:00:00Z",
    "schemaVersion": "1.0.0",
    "evidenceItemCount": 9,
    "attestationCount": 1,
    "auditTrailReferenceCount": 1,
    "isReleaseGradeEvidence": true,
    "releaseGradeNote": "Release-grade: ...",
    "freshnessStatus": "Fresh",
    "activePolicyVersion": "2026.03.07.1",
    "payloadHash": "a3b4c5d6..."
  },
  "remediationGuidance": [],
  "items": [...],
  "totalCount": 9,
  "assembledAt": "2026-03-14T21:00:00Z",
  "schemaVersion": "1.0.0"
}
```

---

## Export Formats

### JSON Export (`POST /evidence/export/json`)

Downloads a structured JSON artifact containing the full evidence bundle.

**Request:**
```json
{
  "ownerId": "user@example.com",
  "limit": 100,
  "correlationId": "optional-corr-id"
}
```

**Response:** `application/json` file download

The JSON artifact includes:
- `schemaVersion` – for downstream schema evolution
- `isReleaseGradeEvidence` – explicit release-grade classification
- `releaseGradeNote` – human-readable explanation
- `freshnessStatus` – evidence freshness
- `policySnapshot` – active policy at export time
- `evidenceItems` – all evidence items with provenance
- `attestationRecords` – attestation metadata
- `auditTrailReferences` – related audit events
- `remediationGuidance` – steps when not release-grade
- `exportManifest` – integrity metadata including SHA-256 payload hash

### CSV Export (`POST /evidence/export/csv`)

Downloads a tabular CSV artifact suitable for spreadsheet review by auditors and compliance operations teams.

**Request:** Same as JSON export.

**Response:** `text/csv` file download

**Sections:**
1. **Header** – Bundle metadata (Release Grade, Policy Version, Freshness)
2. **Evidence Items** – Tabular evidence with columns: `EvidenceId, DecisionId, Category, Source, Timestamp, ValidationStatus, Rationale, DataHash, ExpiresAt`
3. **Attestation Records** (if any) – Attestation metadata
4. **Remediation Guidance** (if not release-grade) – Numbered steps

---

## Fail-Closed Behavior

The API is designed to fail closed rather than provide ambiguous success responses.

| Scenario | Response |
|----------|----------|
| Missing `OwnerId` | `400 Bad Request` with `MISSING_OWNER_ID` error code |
| Invalid token standard | `400 Bad Request` with descriptive error (lists valid standards) |
| Unknown network | `400 Bad Request` with `INVALID_NETWORK` error code |
| No evidence for owner | `200 OK` with `isReleaseGradeEvidence: false` and non-empty `remediationGuidance` |
| Stale evidence (>90 days) | `200 OK` with `freshnessStatus: Stale` and `isReleaseGradeEvidence: false` |
| Invalid limit | `400 Bad Request` with `INVALID_LIMIT` error code |
| Decision not found | `404 Not Found` |
| Unauthenticated | `401 Unauthorized` |

Error responses always include actionable `errorMessage` fields. For example:
- ✅ `"strict sign-off run is older than the current policy revision – policy 2024.01.01.0 is not the current policy 2026.03.07.1"` 
- ❌ ~~`"validation failed"`~~ (too generic, never used)

---

## Decision Trace

Every launch decision produces a structured rule trace accessible via `GET /decision/{id}/trace`. The trace includes:

- **Rule ID** – Stable identifier (e.g., `RULE-KYC-001`)
- **Rule Name** – Human-readable (e.g., `KYC/AML Status Advisory`)
- **Outcome** – `Pass`, `Warning`, `Fail`, `Skipped`
- **Rationale** – Why the rule produced this outcome
- **RemediationGuidance** – What to do when outcome is `Fail` or `Warning`
- **EvaluationOrder** – Deterministic sequence (useful for stable audit traces)
- **InputSnapshot** – Non-sensitive inputs evaluated by the rule

The rule evaluation order is deterministic across replays, enabling stable audit trail references.

---

## Idempotency

Supply an `idempotencyKey` to ensure repeated calls with the same inputs return the cached decision:

```json
{
  "ownerId": "user@example.com",
  "tokenStandard": "ASA",
  "network": "testnet",
  "idempotencyKey": "launch-intent-2026-03-14-v1"
}
```

Use `forceRefresh: true` to bypass the cache and produce a fresh evaluation.

---

## Compliance Rules

The evaluation engine runs **9 deterministic rules** in fixed order:

| Order | Rule ID | Name | Category | Fails When |
|-------|---------|------|----------|------------|
| 1 | `RULE-OWNER-001` | Owner Identity Validation | Identity | OwnerId is absent |
| 2 | `RULE-STANDARD-001` | Token Standard Eligibility | Policy | Unknown token standard |
| 3 | `RULE-NETWORK-001` | Network Eligibility | Integration | Network unhealthy; Mainnet → Warning |
| 4 | `RULE-ENTITLE-001` | Subscription Entitlement Check | Entitlement | ARC1400 without Premium subscription |
| 5 | `RULE-KYC-001` | KYC/AML Status Advisory | Identity | Mainnet-like network → Warning |
| 6 | `RULE-JURIS-001` | Jurisdiction Compliance | Jurisdiction | Jurisdiction violation |
| 7 | `RULE-WL-001` | Whitelist Configuration | Workflow | Invalid whitelist config |
| 8 | `RULE-INTEG-001` | Integration Health | Integration | Service integration unhealthy |
| 9 | `RULE-POLICY-001` | Policy Version Staleness | Policy | Non-current policy → Warning |

---

## Frontend Consumption Pattern

```javascript
// 1. Evaluate launch readiness
const decision = await POST('/api/v1/compliance-evidence/decision', {
  ownerId: userEmail,
  tokenStandard: 'ASA',
  network: 'testnet',
  idempotencyKey: launchIntentId
});

// 2. Check release-grade status before proceeding
if (!decision.isReleaseGradeEvidence) {
  showRemediationSteps(decision.blockers, decision.warnings);
  return;
}

// 3. Retrieve full evidence bundle for display
const bundle = await POST('/api/v1/compliance-evidence/evidence', {
  ownerId: userEmail,
  decisionId: decision.decisionId
});

// 4. Offer downloads for compliance review
const jsonUrl = await POST('/api/v1/compliance-evidence/evidence/export/json', {
  ownerId: userEmail
});
// → triggers download of compliance-evidence-XXXXXXXX-20260314.json
```

---

## Testing

The implementation includes comprehensive test coverage in:

- **`BiatecTokensTests/ComplianceEvidenceLaunchDecisionServiceTests.cs`** – 58 unit tests covering:
  - Input validation (fail-closed for all bad inputs)
  - Bundle assembly (all fields present and correct)
  - Release-grade classification (blocked/stale/fresh scenarios)
  - Fail-closed behavior (empty bundles, missing evidence)
  - JSON export generation (schema contract, integrity hash)
  - CSV export generation (headers, metadata, remediation section)
  - Idempotency (3-run determinism, ForceRefresh bypass)
  - Decision retrieval and listing
  - CorrelationId propagation
  - Schema version contract stability

- **`BiatecTokensTests/ComplianceEvidenceLaunchDecisionIntegrationTests.cs`** – 22 HTTP integration tests covering:
  - Authentication/authorization for all endpoints
  - Full HTTP pipeline validation
  - Release-grade fields in responses
  - JSON/CSV file download endpoints
  - Fail-closed HTTP responses
  - Content-Type headers

---

## Schema Evolution

The `schemaVersion: "1.0.0"` field in all responses provides backward-compatibility signaling. Future breaking changes will increment this version. Frontend consumers should:

1. Read `schemaVersion` before parsing the response
2. Use stable field names listed above (not internal implementation names)
3. Handle missing optional fields gracefully (e.g., `policySnapshot` may be null for historical records)

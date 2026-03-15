# Approval Workflow API

Enterprise approval workflow and release-evidence APIs for multi-stage release pipeline management.

## Overview

The Approval Workflow API enables enterprise teams to enforce structured, auditable multi-stage release approvals before a release package can be marked `LaunchReady`. It provides:

- **Five mandatory approval stages** (Compliance, Legal, Procurement, Executive, SharedOperations)
- **Deterministic posture calculation** — release readiness is calculated from stage decisions and evidence freshness
- **Evidence readiness synthesis** — each stage produces a release-blocking evidence item evaluated for freshness
- **Tamper-evident audit history** — all decisions are immutably recorded with actor identity, rationale, timestamps, and correlation IDs
- **Active owner domain routing** — the API identifies which team currently owns the next required action, enabling frontend role-based views

---

## Stage Types

| Stage | Value | Owning Domain | Description |
|---|---|---|---|
| `Compliance` | 0 | Compliance | KYC/AML identity verification and regulatory sign-off |
| `Legal` | 1 | Legal | Contract, IP, and jurisdictional review |
| `Procurement` | 2 | Procurement | Vendor cost and contract approval |
| `Executive` | 3 | Executive | Executive sponsor authorisation |
| `SharedOperations` | 4 | SharedOperations | Infrastructure, security, and runbook readiness |

All 5 stages must reach `Approved` status before a package can be `LaunchReady`.

---

## Decision Statuses

| Status | Value | Note Required | Description |
|---|---|---|---|
| `Pending` | 0 | — | Stage has not been acted on (default; cannot be submitted) |
| `Approved` | 1 | Optional | Stage sign-off complete |
| `Rejected` | 2 | **Required** | Stage rejected; release blocked |
| `Blocked` | 3 | **Required** | Externally blocked pending resolution |
| `NeedsFollowUp` | 4 | **Required** | Requestor must supply additional information |

> **Important**: You cannot submit `Pending` as a decision. Use `Approved`, `Rejected`, `Blocked`, or `NeedsFollowUp`.

### State Transitions

```
Initial state: Pending
Pending → Approved       (any authorised actor)
Pending → Rejected       (requires Note)
Pending → Blocked        (requires Note)
Pending → NeedsFollowUp  (requires Note)
Approved → Rejected      (re-submit; requires Note)
Approved → Blocked       (re-submit; requires Note)
Rejected → Approved      (re-submit after resolution)
Blocked  → Approved      (re-submit after resolution)
```

Each re-submission creates a new decision record; only the latest decision per stage is the effective status.

---

## Release Posture

The release posture is derived deterministically from stage decisions and evidence readiness. The rules are evaluated in priority order:

| Priority | Rule | Posture |
|---|---|---|
| 1 (highest) | Any stage is `Rejected` | `BlockedByStageDecision` |
| 2 | Any release-blocking evidence is `Missing` | `BlockedByMissingEvidence` |
| 3 | Any release-blocking evidence is `Stale` | `BlockedByStaleEvidence` |
| 4 | Any release-blocking evidence is `ConfigurationBlocked` | `ConfigurationBlocked` |
| 5 | All 5 stages are `Approved` | `LaunchReady` |
| 6 (default) | Any stage is `Pending`, `Blocked`, or `NeedsFollowUp` | `BlockedByStageDecision` |

> The `PostureRationale` field in `ApprovalWorkflowStateResponse` always contains a human-readable explanation of why the current posture was assigned.

---

## Evidence Readiness Categories

Each approval stage produces one evidence item. Evidence is synthesised from stage status and decision age:

| Category | Value | Meaning |
|---|---|---|
| `Fresh` | 0 | Stage `Approved` within the last 30 days |
| `Stale` | 1 | Stage `Approved` but more than 30 days ago |
| `Missing` | 2 | Stage is `Pending`, `Rejected`, `Blocked`, or `NeedsFollowUp` |
| `ConfigurationBlocked` | 3 | Evidence source is not reachable (not currently used in stage-derived evidence) |

Evidence items mapped to stages:

| Evidence ID | Stage | Name |
|---|---|---|
| `evidence-compliance` | Compliance | KYC/AML Identity Verification |
| `evidence-legal` | Legal | Legal Review Sign-off |
| `evidence-procurement` | Procurement | Procurement Approval |
| `evidence-executive` | Executive | Executive Sponsor Sign-off |
| `evidence-sharedoperations` | SharedOperations | Shared Operations Readiness |

---

## Active Owner Domain

The `ActiveOwnerDomain` field identifies which team currently owns the next required action:

| Domain | Value | When Active |
|---|---|---|
| `None` | 0 | All stages approved |
| `Compliance` | 1 | Compliance stage is the first non-approved stage |
| `Legal` | 2 | Legal stage is the first non-approved stage |
| `Procurement` | 3 | Procurement stage is the first non-approved stage |
| `Executive` | 4 | Executive stage is the first non-approved stage |
| `SharedOperations` | 5 | SharedOperations stage is the first non-approved stage |
| `Requestor` | 6 | Any stage is `NeedsFollowUp` |
| `Platform` | 7 | Any evidence is `ConfigurationBlocked` |

> **Frontend guidance**: Use `ActiveOwnerDomain` to highlight the responsible team's action queue. Do not surface actions to teams who don't own the current step.

---

## API Endpoints

Base path: `/api/v1/approval-workflow`

All data endpoints require a valid JWT bearer token (`Authorization: Bearer <token>`).

---

### GET `/api/v1/approval-workflow/{releasePackageId}`

Returns the full approval workflow state for a release package.

**Path parameters:**
- `releasePackageId` — unique release package identifier (URL-encoded)

**Response 200 — `ApprovalWorkflowStateResponse`:**
```json
{
  "success": true,
  "releasePackageId": "pkg-20240101-mytoken",
  "stages": [
    {
      "stageType": "Compliance",
      "status": "Approved",
      "ownerDomain": "Compliance",
      "decidedBy": "compliance@example.com",
      "decidedAt": "2024-01-15T10:30:00Z",
      "note": null,
      "blockerIds": [],
      "updatedAt": "2024-01-15T10:30:00Z"
    },
    {
      "stageType": "Legal",
      "status": "Pending",
      "ownerDomain": "Legal",
      "decidedBy": null,
      "decidedAt": null,
      "note": null,
      "blockerIds": [],
      "updatedAt": "2024-01-15T09:00:00Z"
    }
  ],
  "releasePosture": "BlockedByStageDecision",
  "activeOwnerDomain": "Legal",
  "activeBlockers": [
    {
      "blockerId": "550e8400-e29b-41d4-a716-446655440000",
      "reason": "Evidence 'Legal Review Sign-off' is Missing.",
      "severity": "High",
      "linkedStageType": null,
      "linkedEvidenceId": "evidence-legal",
      "attribution": "None",
      "createdAt": "2024-01-15T09:00:00Z",
      "resolvedAt": null
    }
  ],
  "evidenceSummary": [
    {
      "evidenceId": "evidence-compliance",
      "name": "KYC/AML Identity Verification",
      "category": "Compliance",
      "readinessCategory": "Fresh",
      "lastCheckedAt": "2024-01-15T10:30:00Z",
      "expiresAt": "2024-02-14T10:30:00Z",
      "description": "Anti-money-laundering and know-your-customer identity check sign-off.",
      "isReleaseBlocking": true,
      "configurationNote": null
    }
  ],
  "postureCalculatedAt": "2024-01-15T12:00:00Z",
  "errorCode": null,
  "errorMessage": null,
  "correlationId": "corr-abc-123",
  "postureRationale": "Stage 'Legal' has not been approved. 4 stage(s) Pending..."
}
```

**Response 400** — missing or invalid `releasePackageId`.

---

### POST `/api/v1/approval-workflow/{releasePackageId}/stages/decision`

Submits an approval stage decision.

**Path parameters:**
- `releasePackageId` — unique release package identifier

**Request body — `SubmitStageDecisionRequest`:**
```json
{
  "stageType": "Legal",
  "decision": "Approved",
  "note": "All contracts reviewed and cleared.",
  "evidenceAcknowledgements": []
}
```

**Validation rules:**
- `decision` must not be `Pending`
- `note` is required when `decision` is `Rejected`, `Blocked`, or `NeedsFollowUp`

**Response 200 — `SubmitStageDecisionResponse`:**
```json
{
  "success": true,
  "decisionId": "d9bfef12-1234-5678-abcd-ef0123456789",
  "updatedStage": {
    "stageType": "Legal",
    "status": "Approved",
    "ownerDomain": "Legal",
    "decidedBy": "legal@example.com",
    "decidedAt": "2024-01-15T11:00:00Z",
    "note": "All contracts reviewed and cleared.",
    "blockerIds": [],
    "updatedAt": "2024-01-15T11:00:00Z"
  },
  "newReleasePosture": "BlockedByStageDecision",
  "errorCode": null,
  "errorMessage": null,
  "operatorGuidance": null,
  "correlationId": "corr-abc-124"
}
```

**Response 400 example (missing note on rejection):**
```json
{
  "success": false,
  "decisionId": null,
  "updatedStage": null,
  "newReleasePosture": null,
  "errorCode": "MISSING_REQUIRED_FIELD",
  "errorMessage": "A Note is required when Decision is Rejected.",
  "operatorGuidance": "Provide a rationale in the Note field when submitting a Rejected decision.",
  "correlationId": "corr-abc-125"
}
```

---

### GET `/api/v1/approval-workflow/{releasePackageId}/evidence-summary`

Returns the evidence readiness summary for a release package.

**Response 200 — `ReleaseEvidenceSummaryResponse`:**
```json
{
  "success": true,
  "releasePackageId": "pkg-20240101-mytoken",
  "evidenceItems": [
    {
      "evidenceId": "evidence-compliance",
      "name": "KYC/AML Identity Verification",
      "category": "Compliance",
      "readinessCategory": "Fresh",
      "lastCheckedAt": "2024-01-15T10:30:00Z",
      "expiresAt": "2024-02-14T10:30:00Z",
      "description": "Anti-money-laundering and know-your-customer identity check sign-off.",
      "isReleaseBlocking": true,
      "configurationNote": null
    }
  ],
  "overallReadiness": "Missing",
  "freshCount": 1,
  "staleCount": 0,
  "missingCount": 4,
  "configurationBlockedCount": 0,
  "errorCode": null,
  "errorMessage": null,
  "correlationId": "corr-abc-126"
}
```

---

### GET `/api/v1/approval-workflow/{releasePackageId}/audit-history`

Returns the audit event history for a release package (newest-first, up to 100 events).

**Response 200 — `ApprovalAuditHistoryResponse`:**
```json
{
  "success": true,
  "releasePackageId": "pkg-20240101-mytoken",
  "events": [
    {
      "eventId": "evt-001",
      "eventType": "StageDecisionSubmitted",
      "releasePackageId": "pkg-20240101-mytoken",
      "stageType": "Compliance",
      "actorId": "compliance@example.com",
      "actorDisplayName": "compliance@example.com",
      "timestamp": "2024-01-15T10:30:00Z",
      "description": "Stage Compliance decision: Approved",
      "previousStatus": "Pending",
      "newStatus": "Approved",
      "note": "",
      "correlationId": "corr-abc-123",
      "metadata": {
        "DecisionId": "d9bfef12-1234-5678-abcd-ef0123456789"
      }
    }
  ],
  "totalCount": 1,
  "errorCode": null,
  "errorMessage": null,
  "correlationId": "corr-abc-127"
}
```

---

### GET `/api/v1/approval-workflow/health`

Anonymous health check endpoint for infrastructure monitoring.

**Response 200:**
```json
{
  "status": "Healthy",
  "service": "ApprovalWorkflow",
  "timestamp": "2024-01-15T12:00:00Z"
}
```

---

## Failure Semantics

The Approval Workflow API is **fail-closed by design**:

- Any stage `Rejected` → `BlockedByStageDecision` (overrides all other rules)
- Missing evidence → `BlockedByMissingEvidence` (checked before stale/config)
- Stale evidence → `BlockedByStaleEvidence`
- ConfigurationBlocked evidence → `ConfigurationBlocked`
- Only when ALL 5 stages are `Approved` AND all evidence is `Fresh` → `LaunchReady`

### Authentication

- All data endpoints require `Authorization: Bearer <jwt>` header
- Missing or invalid token → HTTP 401
- The `actorId` is extracted from JWT claims (`NameIdentifier`, `nameid`, or `sub`)
- If no claim is found, `actorId` defaults to `"anonymous"` (will still process the request)

### Correlation IDs

- Pass `X-Correlation-Id: <uuid>` header for distributed tracing
- If not provided, a new UUID is generated per request
- The `correlationId` field is echoed back in every response

---

## Frontend Integration Guide

### Displaying Workflow Status

1. Call `GET /api/v1/approval-workflow/{releasePackageId}` on page load
2. Use `releasePosture` to show a top-level status badge:
   - `LaunchReady` → green badge ✅
   - `BlockedByStageDecision` → red badge ❌
   - `BlockedByStaleEvidence` → orange badge ⚠️
   - `BlockedByMissingEvidence` → orange badge ⚠️
   - `ConfigurationBlocked` → red badge with wrench icon 🔧
3. Use `postureRationale` for the tooltip or inline explanation text
4. Use `activeOwnerDomain` to highlight which team's card/queue is currently active
5. Show `activeBlockers` as a list of actionable items with severity badges

### Stage Cards

For each stage in `stages`:
- Show status chip: `Pending` (grey) / `Approved` (green) / `Rejected` (red) / `Blocked` (orange) / `NeedsFollowUp` (yellow)
- Show `decidedBy` and `decidedAt` for approved stages
- Show `note` for rejected/blocked stages
- Enable "Submit Decision" button only for users belonging to the stage's `ownerDomain`

### Submitting Decisions

```typescript
const response = await fetch(`/api/v1/approval-workflow/${packageId}/stages/decision`, {
  method: 'POST',
  headers: {
    'Content-Type': 'application/json',
    'Authorization': `Bearer ${jwtToken}`,
    'X-Correlation-Id': correlationId,
  },
  body: JSON.stringify({
    stageType: 'Legal',
    decision: 'Approved',
    note: 'Reviewed and approved.',
    evidenceAcknowledgements: [],
  }),
});
```

If `success` is `false`, surface `errorMessage` and `operatorGuidance` to the user.

### Evidence Panel

1. Call `GET /api/v1/approval-workflow/{releasePackageId}/evidence-summary`
2. Show `overallReadiness` as a header badge
3. For each item in `evidenceItems`:
   - `Fresh` → green chip with `expiresAt` tooltip
   - `Stale` → orange chip with "re-approve {stageName} to refresh" tooltip
   - `Missing` → red chip with "approve {stageName} stage" call to action
   - `ConfigurationBlocked` → red chip with `configurationNote`

---

## Retry and Idempotency

- Stage decisions are **idempotent at the decision level** but **not at the record level** — each submission creates a new record. The latest decision per stage is the effective status.
- Network retries are safe: submitting the same decision twice results in two records with the same status; the posture is unchanged.
- Use `decisionId` from the response to link client-side records to server-side audit events.

---

## Storage

The default implementation uses an in-memory `ConcurrentDictionary`. Data is not persisted across application restarts. To add persistence, implement `IApprovalWorkflowRepository` backed by your preferred database and register it in `Program.cs`.

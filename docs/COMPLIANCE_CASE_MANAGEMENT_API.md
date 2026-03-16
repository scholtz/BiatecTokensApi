# Compliance Case Management & Ongoing Monitoring API

## Overview

The Compliance Case Management API provides enterprise-grade lifecycle management for compliance review work. It enables regulated issuers to track investor onboarding exceptions, manage remediation tasks, route escalations, and operate ongoing monitoring programs — all through a durable, auditable service layer.

This API models compliance decisions as explicit domain objects rather than implicit UI state, providing the operational backbone required for MICA-oriented regulated token issuance.

**Base path:** `/api/v1/compliance-cases`

**Authentication:** All endpoints require Bearer JWT authentication.

---

## Compliance Case Lifecycle

```
Intake → EvidencePending → UnderReview → Approved (terminal)
                                        → Rejected  (terminal)
                                        → Escalated → UnderReview
                                        → Remediating → UnderReview
                         → Stale → EvidencePending
       → Blocked → Intake
```

### States

| State | Description |
|---|---|
| `Intake` | Case created, awaiting initial processing |
| `EvidencePending` | Waiting for required evidence from subject |
| `UnderReview` | Under active compliance review |
| `Escalated` | Escalated due to a compliance concern (sanctions, watchlist) |
| `Remediating` | Has open remediation tasks to resolve |
| `Approved` | **Terminal** — subject approved |
| `Rejected` | **Terminal** — subject rejected |
| `Stale` | Evidence has expired; re-submission required |
| `Blocked` | Blocked pending manual intervention |

---

## Endpoints

### Create a Compliance Case

```
POST /api/v1/compliance-cases
```

Creates a new compliance case. **Idempotent**: if an active case for the same `(issuerId, subjectId, type)` already exists, that case is returned.

**Request body:**
```json
{
  "issuerId": "issuer-123",
  "subjectId": "investor-456",
  "type": "InvestorEligibility",
  "priority": "High",
  "jurisdiction": "US",
  "externalReference": "crm-case-789",
  "correlationId": "corr-abc123",
  "linkedDecisionIds": ["decision-001"]
}
```

**Case types:** `InvestorEligibility`, `LaunchPackage`, `OngoingMonitoring`

**Response:** `200 OK` with `CreateComplianceCaseResponse` or `400 Bad Request` on validation failure.

---

### Get a Compliance Case

```
GET /api/v1/compliance-cases/{caseId}
```

Retrieves a single case. Automatically checks evidence freshness and transitions to `Stale` if evidence has expired.

**Response:** `200 OK`, `404 Not Found`

---

### List Compliance Cases

```
POST /api/v1/compliance-cases/list
```

Returns a paginated, filtered list of cases suitable for queue-style front-end rendering.

**Request body:**
```json
{
  "issuerId": "issuer-123",
  "state": "UnderReview",
  "priority": "High",
  "assignedReviewerId": "reviewer-001",
  "jurisdiction": "EU",
  "caseType": "InvestorEligibility",
  "onlyStale": false,
  "pageSize": 25,
  "pageToken": null
}
```

All filter fields are optional. Returns `ListComplianceCasesResponse` with `TotalCount` and pagination cursor.

---

### Update a Compliance Case

```
PATCH /api/v1/compliance-cases/{caseId}
```

Updates mutable metadata: `Priority`, `AssignedReviewerId`, `Jurisdiction`, `ExternalReference`.

---

### Transition Case State

```
POST /api/v1/compliance-cases/{caseId}/transition
```

Moves a case to a new lifecycle state. Invalid transitions are rejected with `400 Bad Request`.

```json
{
  "newState": "UnderReview",
  "reason": "All required evidence collected"
}
```

---

### Add Evidence

```
POST /api/v1/compliance-cases/{caseId}/evidence
```

Attaches a normalized evidence summary to a case. Does not store raw provider payloads.

```json
{
  "evidenceType": "KYC",
  "status": "Valid",
  "providerName": "StripeIdentity",
  "providerReference": "si_ref_abc123",
  "capturedAt": "2026-03-01T12:00:00Z",
  "expiresAt": "2026-09-01T12:00:00Z",
  "summary": "Identity verified. No issues found.",
  "isBlockingReadiness": false
}
```

---

### Add Remediation Task

```
POST /api/v1/compliance-cases/{caseId}/remediation-tasks
```

Creates an actionable task that may block case readiness until resolved.

```json
{
  "title": "Verify source of funds",
  "description": "Investor must provide bank statements or proof of income",
  "ownerId": "reviewer-001",
  "dueAt": "2026-04-01T00:00:00Z",
  "isBlockingCase": true,
  "blockerSeverity": "High"
}
```

---

### Resolve a Remediation Task

```
POST /api/v1/compliance-cases/{caseId}/remediation-tasks/{taskId}/resolve
```

```json
{
  "status": "Resolved",
  "resolutionNotes": "Bank statements verified. Source of funds confirmed legitimate."
}
```

---

### Add Escalation

```
POST /api/v1/compliance-cases/{caseId}/escalations
```

Raises a compliance escalation (sanctions hit, watchlist match, jurisdiction conflict, etc.).

```json
{
  "type": "SanctionsHit",
  "description": "OFAC SDN match at 0.92 confidence",
  "screeningSource": "ComplyAdvantage",
  "matchedCategories": ["Sanctions", "OFAC"],
  "confidenceScore": 0.92,
  "requiresManualReview": true
}
```

**Escalation types:** `SanctionsHit`, `WatchlistMatch`, `JurisdictionConflict`, `AdverseMedia`, `ManualEscalation`

---

### Resolve an Escalation

```
POST /api/v1/compliance-cases/{caseId}/escalations/{escalationId}/resolve
```

```json
{
  "resolutionNotes": "Match confirmed as false positive — different person with same name."
}
```

---

### Get Case Timeline

```
GET /api/v1/compliance-cases/{caseId}/timeline
```

Returns the full chronological audit trail of events for a case.

**Timeline event types:**
- `CaseCreated`, `StateTransition`, `EvidenceAdded`, `EvidenceStale`
- `EscalationRaised`, `EscalationResolved`
- `RemediationTaskAdded`, `RemediationTaskResolved`
- `ReviewerAssigned`, `ReviewerNoteAdded`, `ReadinessChanged`
- `MonitoringScheduleSet`, `MonitoringReviewRecorded`, `MonitoringFollowUpCreated`

---

### Get Case Readiness Summary

```
GET /api/v1/compliance-cases/{caseId}/readiness
```

Evaluates whether a case is ready to proceed using **fail-closed semantics**:
- No evidence captured → `IsReady: false`, `FailedClosed: true`
- Open blocking remediation tasks → `IsReady: false`
- Open critical escalations requiring manual review → `IsReady: false`
- All clear → `IsReady: true`

---

## Ongoing Monitoring Endpoints

Ongoing monitoring supports post-onboarding periodic review programs. After a subject is approved, operators can enroll their case in a monitoring schedule and record structured review outcomes.

### Set Monitoring Schedule

```
POST /api/v1/compliance-cases/{caseId}/monitoring-schedule
```

Configures a periodic review schedule for a case. Can be applied to cases in any state.
Typically used after a case is approved to enroll the subject in a post-onboarding monitoring program.

```json
{
  "frequency": "SemiAnnual",
  "customIntervalDays": null,
  "notes": "MICA periodic monitoring requirement"
}
```

**Frequency presets:**

| Value | Interval |
|---|---|
| `Monthly` | 30 days |
| `Quarterly` | 90 days |
| `SemiAnnual` | 180 days |
| `Annual` | 365 days |
| `Custom` | `customIntervalDays` value (required, > 0) |

**Response** includes the `MonitoringSchedule` object with:
- `IntervalDays` — resolved interval
- `NextReviewDueAt` — calculated next review date
- `IsOverdue` — whether review is past due
- `CreatedAt` / `LastReviewAt`

---

### Record Monitoring Review

```
POST /api/v1/compliance-cases/{caseId}/monitoring-reviews
```

Records the outcome of a periodic monitoring review. **Requires an active monitoring schedule** (call `SetMonitoringSchedule` first).

```json
{
  "outcome": "EscalationRequired",
  "reviewNotes": "Re-screening returned a new OFAC sanctions hit at 0.95 confidence.",
  "createFollowUpCase": true,
  "attributes": {
    "watchlist": "OFAC",
    "matchScore": "0.95",
    "rescreenProvider": "ComplyAdvantage"
  }
}
```

**Outcomes:**

| Value | Meaning |
|---|---|
| `Clear` | No concerns; subject remains in good standing |
| `AdvisoryNote` | Minor changes observed; monitoring continues |
| `ConcernIdentified` | Concern noted; does not require immediate action |
| `EscalationRequired` | Critical finding requiring escalation |

When `outcome` is `EscalationRequired` and `createFollowUpCase` is `true`, a new `OngoingMonitoring` case is automatically created with `Priority: High` and linked to the review record via `FollowUpCaseId`.

**Response** includes:
- `Review` — the recorded `MonitoringReview` record
- `Case` — the updated case with refreshed schedule timestamps
- `FollowUpCase` — the new follow-up case (if created)

---

### Trigger Periodic Review Check

```
POST /api/v1/compliance-cases/periodic-review-check
```

Scans all cases with active monitoring schedules and marks any overdue reviews. Intended for invocation from a scheduled background job or cron trigger.

**Response:**
```json
{
  "success": true,
  "casesInspected": 42,
  "overdueCasesFound": 3,
  "overdueCaseIds": ["case-aaa", "case-bbb", "case-ccc"],
  "checkedAt": "2026-03-16T04:00:00Z"
}
```

Overdue cases have their `MonitoringSchedule.IsOverdue` flag set to `true`, making them surfaceable in frontend queues filtered by overdue status.

---

## Design Principles

### Fail-Closed Readiness

Case readiness evaluation is always **fail-closed**: the system returns `IsReady: false` whenever required preconditions are not met, rather than assuming readiness in ambiguous states.

### Idempotent Case Creation

`POST /compliance-cases` is idempotent by `(issuerId, subjectId, type)`. Concurrent or duplicate triggers (e.g., from KYC/AML callbacks) will return the existing active case rather than creating duplicates.

### Audit-First Timeline

Every material state change creates an immutable `CaseTimelineEntry` with `EventType`, `ActorId`, `OccurredAt`, and human-readable `Description`. The timeline supports:
- Regulator review and export
- Operator decision attribution
- Support and debug investigation
- Frontend activity history rendering

### Evidence Freshness

The service automatically detects expired evidence on `GetCase` and `RunEvidenceFreshnessCheck`. Cases in `EvidencePending` state with expired evidence are transitioned to `Stale` automatically, surfacing them in operator queues without manual intervention.

### Authorization

All endpoints require authenticated access (`[Authorize]`). Actor identity is extracted from JWT claims and recorded in every timeline entry, ensuring full operator accountability.

---

## Integration with Existing Services

| Downstream Service | Integration Point |
|---|---|
| `ComplianceOrchestrationService` | Create cases from KYC/AML provider callbacks via `LinkedDecisionIds` |
| `KycAmlDecisionIngestionService` | Link ingestion decisions to cases using `ExternalReference` |
| `ApprovalWorkflowService` | Cases in `Approved` state can enrol in monitoring programs |
| `WebhookService` | Future: emit `ComplianceCaseUpdated` webhook events on state transitions |
| `RegulatoryEvidencePackageService` | Evidence items from cases can feed into regulatory evidence packages |

---

## Test Coverage

Tests are in `BiatecTokensTests/ComplianceCaseManagementTests.cs` (105 tests total):

| Category | Count |
|---|---|
| Unit: case CRUD and validation | 15 |
| Unit: state transition matrix | 20 |
| Unit: evidence, escalations, remediation | 20 |
| Unit: evidence freshness / FakeTimeProvider | 5 |
| Unit: monitoring schedule | 10 |
| Unit: monitoring review recording | 10 |
| Unit: periodic review check | 5 |
| Integration: HTTP pipeline (WebApplicationFactory) | 20 |
| **Total** | **105** |

Run targeted tests:
```bash
dotnet test --filter "FullyQualifiedName~ComplianceCaseManagement" --configuration Release
```

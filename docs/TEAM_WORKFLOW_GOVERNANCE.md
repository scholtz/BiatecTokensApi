# Team Workflow Governance — Role Model & Audit API

## Overview

The `IssuerWorkflowService` provides enterprise-grade, server-side role enforcement and durable audit evidence for all team approval operations in the Biatec Tokens platform.

This document covers:

- [Role model](#role-model)
- [Workflow state machine](#workflow-state-machine)
- [Action authorisation matrix](#action-authorisation-matrix)
- [API reference](#api-reference)
- [Audit record semantics](#audit-record-semantics)
- [Permissions discovery](#permissions-discovery)
- [Failure semantics](#failure-semantics)
- [Tenant isolation](#tenant-isolation)
- [Bootstrap procedure](#bootstrap-procedure)
- [Deferred / future scope](#deferred--future-scope)

---

## Role model

All team governance operations are scoped to an **issuer** (a token-issuing organisation). Every user who needs to act within an issuer workspace must be explicitly added to the team with one of the following roles.

| Role | Enum value | Description |
|---|---|---|
| `Operator` | 0 | Creates, submits, and completes workflow items. Performs operational tasks. |
| `ComplianceReviewer` | 1 | Reviews compliance aspects and policy changes. May approve, reject, or request changes. |
| `FinanceReviewer` | 2 | Reviews financial and economic implications. May approve, reject, or request changes. |
| `Admin` | 3 | Full administrative rights: manages team membership and performs all other actions. |
| `ReadOnlyObserver` | 4 | Read-only access to view workflow state and audit history. Cannot act. |

### Design principles

- **Least-privilege**: ReadOnlyObserver explicitly cannot mutate state.
- **Separation of duties**: Operators create/submit; Reviewers approve/reject; Admin bridges both.
- **Fail-closed**: Unrecognised users are treated as non-members and receive UNAUTHORIZED.

---

## Workflow state machine

```
  ┌─────────────────────────────────────────────────────────────────────────┐
  │                           Workflow lifecycle                            │
  │                                                                         │
  │                                                                         │
  │   Created ──► Prepared ──────────────────► PendingReview               │
  │     (Operator)              (submit)         (any reviewer acts)        │
  │                                               /        |       \        │
  │                                        Approved   Rejected   NeedsChanges
  │                                           |                      |      │
  │                                        Completed        (Operator       │
  │                                                          resubmits)     │
  │                                                               |          │
  │                                                         PendingReview   │
  └─────────────────────────────────────────────────────────────────────────┘
```

### Allowed transitions

| From | To | Permitted? | Required role |
|---|---|---|---|
| `Prepared` | `PendingReview` | ✅ | Operator, Admin |
| `PendingReview` | `Approved` | ✅ | ComplianceReviewer, FinanceReviewer, Admin |
| `PendingReview` | `Rejected` | ✅ | ComplianceReviewer, FinanceReviewer, Admin |
| `PendingReview` | `NeedsChanges` | ✅ | ComplianceReviewer, FinanceReviewer, Admin |
| `Approved` | `Completed` | ✅ | Operator, Admin |
| `NeedsChanges` | `PendingReview` | ✅ | Operator, Admin (resubmit) |
| `Rejected` | (any) | ❌ | Terminal state — no further transitions |
| `Completed` | (any) | ❌ | Terminal state — no further transitions |

Any attempt to perform an invalid transition returns HTTP **400 Bad Request** with error code `INVALID_STATE_TRANSITION` and a human-readable reason explaining which transitions are valid from the current state.

---

## Action authorisation matrix

| Action | Operator | ComplianceReviewer | FinanceReviewer | Admin | ReadOnlyObserver |
|---|---|---|---|---|---|
| Add / update / remove team members | ❌ | ❌ | ❌ | ✅ | ❌ |
| View team members | ✅ | ✅ | ✅ | ✅ | ✅ |
| Create workflow item | ✅ | ❌ | ❌ | ✅ | ❌ |
| Submit for review | ✅ | ❌ | ❌ | ✅ | ❌ |
| Approve | ❌ | ✅ | ✅ | ✅ | ❌ |
| Reject | ❌ | ✅ | ✅ | ✅ | ❌ |
| Request changes | ❌ | ✅ | ✅ | ✅ | ❌ |
| Resubmit after changes | ✅ | ❌ | ❌ | ✅ | ❌ |
| Complete | ✅ | ❌ | ❌ | ✅ | ❌ |
| Reassign | ✅ | ✅ | ✅ | ✅ | ❌ |
| View workflow items / queue | ✅ | ✅ | ✅ | ✅ | ✅ |
| View audit history | ✅ | ✅ | ✅ | ✅ | ✅ |
| View approval summary | ✅ | ✅ | ✅ | ✅ | ✅ |
| View own permissions | (all authenticated users) | | | | |

---

## API reference

Base path: `/api/v1/issuer-workflow`

All endpoints require a valid JWT bearer token (`Authorization: Bearer <token>`).

### Team membership

| Method | Path | Description | Min role |
|---|---|---|---|
| `POST` | `/{issuerId}/members` | Add member (bootstrap: first Admin by any caller) | Admin (or bootstrap) |
| `PUT` | `/{issuerId}/members/{memberId}` | Update role / display name | Admin |
| `DELETE` | `/{issuerId}/members/{memberId}` | Soft-remove member | Admin |
| `GET` | `/{issuerId}/members/{memberId}` | Get single member | Any active member |
| `GET` | `/{issuerId}/members` | List active members | Any active member |

### Workflow items

| Method | Path | Description | Min role |
|---|---|---|---|
| `POST` | `/{issuerId}/workflows` | Create item (Prepared) | Operator, Admin |
| `GET` | `/{issuerId}/workflows/{workflowId}` | Get single item | Any active member |
| `GET` | `/{issuerId}/workflows` | List items | Any active member |

### Workflow transitions

| Method | Path | Transition | Min role |
|---|---|---|---|
| `POST` | `/{issuerId}/workflows/{workflowId}/submit` | Prepared → PendingReview | Operator, Admin |
| `POST` | `/{issuerId}/workflows/{workflowId}/approve` | PendingReview → Approved | ComplianceReviewer, FinanceReviewer, Admin |
| `POST` | `/{issuerId}/workflows/{workflowId}/reject` | PendingReview → Rejected | ComplianceReviewer, FinanceReviewer, Admin |
| `POST` | `/{issuerId}/workflows/{workflowId}/request-changes` | PendingReview → NeedsChanges | ComplianceReviewer, FinanceReviewer, Admin |
| `POST` | `/{issuerId}/workflows/{workflowId}/resubmit` | NeedsChanges → PendingReview | Operator, Admin |
| `POST` | `/{issuerId}/workflows/{workflowId}/reassign` | (any active state) | Non-ReadOnly active member |
| `POST` | `/{issuerId}/workflows/{workflowId}/complete` | Approved → Completed | Operator, Admin |

### Governance queries

| Method | Path | Description | Min role |
|---|---|---|---|
| `GET` | `/{issuerId}/summary` | Approval dashboard summary | Any active member |
| `GET` | `/{issuerId}/queue/{assigneeId}` | Items assigned to specific actor | Any active member |
| `GET` | `/{issuerId}/my-permissions` | Actor permissions snapshot | Authenticated (always 200) |
| `GET` | `/{issuerId}/workflows/{workflowId}/audit-history` | Full audit trail for item | Any active member |

---

## Audit record semantics

Every state transition in the workflow engine produces a `WorkflowAuditEntry` record with the following fields:

| Field | Type | Description |
|---|---|---|
| `entryId` | string (GUID) | Unique, immutable entry identifier |
| `workflowId` | string | Workflow item this entry belongs to |
| `fromState` | enum | State before the transition |
| `toState` | enum | State after the transition |
| `actorId` | string | Identity of the user who caused the transition |
| `note` | string? | Optional rationale (approval note, rejection reason, etc.) |
| `timestamp` | DateTime (UTC) | When the transition occurred |
| `correlationId` | string? | HTTP trace identifier for distributed log correlation |

### Audit design principles

- **Immutable append-only**: entries are never updated or deleted.
- **Deterministic ordering**: entries are ordered by `timestamp` ascending.
- **Actor-attributable**: every entry names the actor, enabling accountability.
- **Correlation-friendly**: `correlationId` matches the HTTP `TraceIdentifier` for cross-system log tracing.
- **Regulator-safe**: fields carry enough context for a compliance officer or auditor to understand the decision chain without reading raw system logs.

A creation entry (Prepared → Prepared) is recorded when an item is first created, so the full lifecycle including provenance is captured from the very beginning.

---

## Permissions discovery

`GET /{issuerId}/my-permissions`

Returns a `ActorPermissions` snapshot for the requesting actor. The payload lists every governance action with:

- `actionKey`: stable machine-readable key (e.g. `"APPROVE"`, `"MANAGE_MEMBERS"`)
- `label`: human-readable label for UI affordances
- `isAllowed`: whether the actor is currently permitted to perform this action
- `deniedReason`: when `isAllowed = false`, a plain-English explanation the frontend can display

**This endpoint always returns HTTP 200**, even for non-members. Non-members receive `isMember = false` and all actions denied. This ensures the frontend can always fetch permissions without special-casing HTTP error codes.

### Example: Admin response (abridged)

```json
{
  "success": true,
  "permissions": {
    "issuerId": "org-abc-123",
    "actorId": "user@example.com",
    "isMember": true,
    "role": 3,
    "permittedActions": [
      { "actionKey": "APPROVE",        "label": "Approve workflow item",   "isAllowed": true,  "deniedReason": null },
      { "actionKey": "MANAGE_MEMBERS", "label": "Add / update / remove team members", "isAllowed": true, "deniedReason": null }
    ],
    "generatedAt": "2026-03-14T09:48:42Z"
  }
}
```

### Example: ReadOnlyObserver response (abridged)

```json
{
  "success": true,
  "permissions": {
    "isMember": true,
    "role": 4,
    "permittedActions": [
      { "actionKey": "APPROVE",        "isAllowed": false, "deniedReason": "Requires ComplianceReviewer, FinanceReviewer, or Admin role." },
      { "actionKey": "MANAGE_MEMBERS", "isAllowed": false, "deniedReason": "Requires Admin role." },
      { "actionKey": "VIEW_AUDIT_HISTORY", "isAllowed": true, "deniedReason": null }
    ]
  }
}
```

---

## Failure semantics

### HTTP status codes

| Code | Meaning |
|---|---|
| 200 OK | Success (or permissions snapshot for any user) |
| 400 Bad Request | Invalid input, duplicate entry, invalid state transition |
| 401 Unauthorized | No or invalid JWT token |
| 403 Forbidden | Authenticated but not authorised (not a member, or insufficient role) |
| 404 Not Found | Team member or workflow item not found |

### Error codes

| Code | Description |
|---|---|
| `UNAUTHORIZED` | Actor is not an active member of the issuer team |
| `INSUFFICIENT_ROLE` | Actor is a member but their role does not permit this operation |
| `INVALID_STATE_TRANSITION` | The requested state transition is not allowed from the current state |
| `NOT_FOUND` | The requested resource does not exist in this issuer scope |
| `DUPLICATE_MEMBER` | The user is already an active member of this issuer team |
| `BOOTSTRAP_ROLE_REQUIRED` | The first member added to an issuer must have the Admin role |
| `MISSING_REQUIRED_FIELD` | A required request field is missing or empty |

### Fail-closed behaviour

- Non-members who call any protected endpoint receive HTTP 403 with `UNAUTHORIZED`.
- Insufficient-role actors receive HTTP 403 with `INSUFFICIENT_ROLE`.
- Invalid state transitions receive HTTP 400 with `INVALID_STATE_TRANSITION`.
- The service **never** silently falls back to a permissive default.

---

## Tenant isolation

Every API path includes `{issuerId}`. The service layer enforces:

1. A user's membership and role are looked up **within the requested issuer**. A user who is Admin for issuer A has no elevated access to issuer B.
2. Workflow items are stored and queried **within their issuer scope**. A member of issuer A cannot read or mutate workflow items belonging to issuer B.
3. Cross-issuer access attempts return HTTP 403 with `UNAUTHORIZED` (same as non-member) to avoid leaking issuer identifiers.

---

## Bootstrap procedure

A new issuer team is bootstrapped as follows:

1. Any authenticated user calls `POST /{issuerId}/members` with `role = Admin (3)`.
2. Because no members exist yet, the call is allowed unconditionally.
3. The bootstrapping user's identity becomes the first Admin.
4. All subsequent membership changes require the Admin role.

**The first member must be Admin.** Attempting to bootstrap with any other role returns HTTP 400 with `BOOTSTRAP_ROLE_REQUIRED`.

---

## Deferred / future scope

The following governance scenarios are intentionally deferred from this iteration:

| Scenario | Rationale | Next recommended step |
|---|---|---|
| Multi-approver quorum (e.g. 2-of-3 sign-off) | Requires additional state tracking and configurable quorum policy per item type | Add `ApprovalQuorumPolicy` to `WorkflowItemType` metadata and track approvals per item |
| Approval expiry / SLA enforcement | Requires background scheduler | Introduce a `WorkflowSlaWorker` with configurable timeout per state |
| Durable / external storage | Currently in-memory; survives only for the process lifetime | Replace `IssuerWorkflowRepository` with database-backed implementation |
| Approval revocation (undo approve before completion) | Not supported; approved items must be rejected and re-submitted | Add an `Approved → PendingReview` transition gate for Admin-only revocation |
| Audit export (regulator-grade PDF/CSV) | Out of scope for this issue | Build on `WorkflowAuditHistoryResponse` to produce a formatted export artifact |
| Role-based notification / webhooks | Out of scope | Extend `WebhookRepository` to subscribe to approval events |

# Compliance Screening Orchestration

## Overview

The Compliance Screening Orchestration layer provides a provider-agnostic backend foundation for KYC (Know Your Customer) and AML (Anti-Money Laundering) screening as first-class steps in the Biatec Tokens issuance workflow. It is designed for enterprise operators and compliance teams who need deterministic, auditable compliance decisions without wallet dependencies or crypto-native complexity.

All endpoints require JWT authentication. The product vision is email/password authentication only; no wallet connectors are involved.

---

## API Endpoints

Base path: `/api/v1/compliance-orchestration`

### POST `/initiate`

Initiates a new compliance screening case for a subject.

**Request:**
```json
{
  "subjectId": "user-001",
  "contextId": "issuance-abc-123",
  "checkType": 2,
  "subjectMetadata": {
    "full_name": "Jane Doe",
    "date_of_birth": "1985-03-15",
    "country": "DE"
  },
  "idempotencyKey": "optional-custom-key"
}
```

| Field | Type | Required | Description |
|---|---|---|---|
| `subjectId` | string | ✅ | Identifier of the subject being checked (e.g. user ID, issuer reference) |
| `contextId` | string | ✅ | Caller-supplied context (e.g. token issuance ID); used for default idempotency derivation |
| `checkType` | int | No | `0`=KYC only, `1`=AML only, `2`=Combined (default) |
| `subjectType` | int | No | `0`=Individual (default), `1`=BusinessEntity |
| `subjectMetadata` | dict | No | Optional metadata forwarded to providers (individual: `full_name`, `date_of_birth`, `country`; business: `legal_name`, `registration_number`, `jurisdiction`, `ultimate_beneficial_owner`) |
| `evidenceValidityHours` | int | No | When set for approved decisions, the response includes `evidenceExpiresAt` set this many hours in the future; `0` = no expiry |
| `idempotencyKey` | string | No | Explicit idempotency key; derived from `subjectId:contextId:checkType` if omitted |

**Response (200 OK):**
```json
{
  "success": true,
  "decisionId": "a1b2c3d4e5f6...",
  "state": 1,
  "reasonCode": null,
  "providerErrorCode": 0,
  "correlationId": "corr-xyz",
  "isIdempotentReplay": false,
  "subjectType": 0,
  "initiatedAt": "2026-03-14T12:00:00Z",
  "completedAt": "2026-03-14T12:00:01Z",
  "evidenceExpiresAt": "2026-04-14T12:00:01Z",
  "matchedWatchlistCategories": [],
  "confidenceScore": null,
  "auditTrail": [ ... ],
  "reviewerNotes": []
}
```

**Error Response (400 Bad Request):**
```json
{
  "success": false,
  "errorCode": "MISSING_REQUIRED_FIELD",
  "errorMessage": "SubjectId is required.",
  "correlationId": "corr-xyz"
}
```

---

### GET `/status/{decisionId}`

Returns the current state of a compliance decision by its ID.

**Response (200 OK):** Same shape as initiate response.

**Response (404 Not Found):**
```json
{
  "success": false,
  "errorCode": "COMPLIANCE_CHECK_NOT_FOUND",
  "errorMessage": "Decision 'xyz' not found."
}
```

---

### GET `/history/{subjectId}`

Returns the full decision history for a subject, ordered by most recent first.

**Response (200 OK):**
```json
{
  "success": true,
  "subjectId": "user-001",
  "decisions": [ ... ],
  "totalCount": 3
}
```

---

### POST `/notes/{decisionId}`

Appends a reviewer note or evidence reference to an existing compliance decision. Notes allow operators to attach human-readable context, document references, or evidence metadata for audit and review purposes.

**Request:**
```json
{
  "content": "Passport scan reviewed manually. Identity confirmed.",
  "evidenceReferences": {
    "passport_scan": "doc-id-abc-123",
    "sanction_list_version": "2026-Q1"
  }
}
```

| Field | Type | Required | Description |
|---|---|---|---|
| `content` | string | ✅ | Free-text reviewer note; must be non-empty |
| `evidenceReferences` | dict | No | Structured evidence labels mapped to identifiers (e.g. document IDs, version strings) |

**Response (200 OK):**
```json
{
  "success": true,
  "note": {
    "noteId": "d1e2f3...",
    "decisionId": "a1b2c3...",
    "actorId": "operator@company.com",
    "content": "Passport scan reviewed manually. Identity confirmed.",
    "evidenceReferences": {
      "passport_scan": "doc-id-abc-123"
    },
    "appendedAt": "2026-03-14T12:05:00Z",
    "correlationId": "corr-xyz"
  },
  "correlationId": "corr-xyz"
}
```

**Error Response (404 Not Found):** Decision not found.

**Error Response (400 Bad Request):** Missing or empty content.

---

## Domain Model

### `ComplianceDecisionState`

| Value | Name | Description |
|---|---|---|
| `0` | `Pending` | Check initiated but not yet complete |
| `1` | `Approved` | Subject passed all compliance checks |
| `2` | `Rejected` | Subject failed one or more compliance checks |
| `3` | `NeedsReview` | Manual review required before a final decision |
| `4` | `Error` | An internal operational error occurred |
| `5` | `ProviderUnavailable` | The screening provider was unreachable; check could not complete (fail-closed) |
| `6` | `Expired` | A previously-approved decision has exceeded its evidence validity window |
| `7` | `InsufficientData` | Required subject data was missing or incomplete; screening could not proceed |

**Terminal states** (set `completedAt`): `Approved`, `Rejected`, `NeedsReview`, `Error`, `ProviderUnavailable`, `InsufficientData`.

**Expiry**: When an `Approved` decision has an `evidenceExpiresAt` timestamp and that timestamp is reached, the decision transitions to `Expired` on next retrieval via `GET /status/{decisionId}`.

### `ScreeningSubjectType`

| Value | Name | Description |
|---|---|---|
| `0` | `Individual` | Natural person (default) |
| `1` | `BusinessEntity` | Legal entity, company, or business organisation |

### `ComplianceCheckType`

| Value | Name | Description |
|---|---|---|
| `0` | `Kyc` | KYC check only |
| `1` | `Aml` | AML screening only |
| `2` | `Combined` | KYC then AML (KYC hard-failure skips AML) |

### `ComplianceProviderErrorCode`

| Value | Name | Description |
|---|---|---|
| `0` | `None` | No error |
| `1` | `Timeout` | Provider call timed out |
| `2` | `ProviderUnavailable` | Provider service unreachable |
| `3` | `MalformedResponse` | Unparseable provider response |
| `4` | `InternalError` | Internal orchestration error |
| `5` | `InvalidRequest` | Invalid request sent to provider |

---

## Decision Lifecycle

```
Initiate → Pending → [KYC + AML runs] → Approved / Rejected / NeedsReview / Error
                                              ↓          ↑
                               ProviderUnavailable / InsufficientData (fail-closed)
                                              ↓
                          Approved (with EvidenceValidityHours) → Expired (on retrieval)
                                              ↓
                        Reviewer appends notes/evidence via POST /notes/{id}
```

### Combined Check (KYC + AML) Priority Rules

1. If KYC returns `Rejected`, `Error`, `ProviderUnavailable`, `InsufficientData`, or `Expired` → AML is skipped; final state mirrors KYC
2. If AML returns `Rejected` → final state is `Rejected`
3. If AML returns `ProviderUnavailable` → final state is `ProviderUnavailable`
4. If AML returns `InsufficientData` → final state is `InsufficientData`
5. If AML returns `Error` → final state is `Error`
6. If either KYC or AML returns `NeedsReview` → final state is `NeedsReview`
7. If both pass → final state is `Approved`

### Idempotency

If a request is re-submitted with the same idempotency key (explicit or derived), the cached result is returned with `isIdempotentReplay: true`. No re-execution occurs.

---

## Audit Trail

Every decision maintains a chronological `auditTrail` list of `ComplianceAuditEvent` records. Events include:

| Event | Description |
|---|---|
| `CheckInitiated` | Decision record created; records `SubjectType` |
| `KycCompleted` | KYC provider call returned |
| `AmlCompleted` | AML provider call returned |
| `AmlSkipped` | AML was skipped due to hard KYC failure |
| `CheckError` | Unexpected exception during execution |
| `ReviewerNoteAppended` | Operator appended a reviewer note |
| `EvidenceExpired` | Evidence validity window has elapsed; decision transitioned to `Expired` |

Each event carries `occurredAt`, `state`, `correlationId`, optional `providerReferenceId`, and a `message`.

---

## Provider Abstraction

### IKycProvider

```csharp
Task<(string providerReferenceId, KycStatus status, string? errorMessage)>
    StartVerificationAsync(string userId, StartKycVerificationRequest request, string correlationId);

Task<(KycStatus status, string? reason, string? errorMessage)>
    FetchStatusAsync(string providerReferenceId);

bool ValidateWebhookSignature(string payload, string signature, string webhookSecret);

Task<(string providerReferenceId, KycStatus status, string? reason)>
    ParseWebhookAsync(KycWebhookPayload payload);
```

### IAmlProvider

```csharp
Task<(string providerReferenceId, ComplianceDecisionState state, string? reasonCode, string? errorMessage)>
    ScreenSubjectAsync(string subjectId, Dictionary<string, string> metadata, string correlationId);

Task<ComplianceDecisionState>
    GetScreeningStatusAsync(string providerReferenceId);
```

### Current Implementations

| Provider | Class | Description |
|---|---|---|
| KYC Mock | `MockKycProvider` | Deterministic mock; controlled by `KycConfig.MockAutoApprove` |
| AML Mock | `MockAmlProvider` | Deterministic mock; flags in metadata control outcomes |

### Mock AML Behavior

Pass these keys in `subjectMetadata` to control the mock AML provider's outcome:

| Key | Value | Outcome | Notes |
|---|---|---|---|
| `sanctions_flag` | `"true"` | `Rejected` | Populates `matchedWatchlistCategories` with `OFAC_SDN`, `EU_SANCTIONS` |
| `review_flag` | `"true"` | `NeedsReview` | Populates `matchedWatchlistCategories` with `PEP_WATCHLIST` |
| `simulate_timeout` | `"true"` | `Error` (Timeout) | |
| `simulate_unavailable` | `"true"` | `ProviderUnavailable` | Distinct from Error; fail-closed |
| `simulate_malformed` | `"true"` | `Error` (MalformedResponse) | |
| `simulate_insufficient_data` | `"true"` | `InsufficientData` | |
| *(none)* | — | `Approved` | |

### Mock KYC Behavior

Controlled by `KycConfig.MockAutoApprove` (appsettings):

- `true` → `Approved`
- `false` → `Pending`

---

## Configuration

```json
{
  "KycConfig": {
    "MockAutoApprove": true
  }
}
```

For production deployments, replace `MockKycProvider` and `MockAmlProvider` with real vendor implementations registered in `Program.cs`:

```csharp
// Current (mock):
builder.Services.AddScoped<IKycProvider, MockKycProvider>();
builder.Services.AddScoped<IAmlProvider, MockAmlProvider>();

// Future (real provider example):
// builder.Services.AddScoped<IKycProvider, StripeIdentityKycProvider>();
// builder.Services.AddScoped<IAmlProvider, ComplyAdvantageAmlProvider>();
```

Provider-specific configuration and API keys should be injected via environment variables or Azure Key Vault, not committed to source code.

---

## Error Semantics

The compliance orchestration layer distinguishes between three categories:

1. **Compliance outcome failures** (`Rejected`, `NeedsReview`): The subject failed or requires human review. These are product-level outcomes with clear compliance meaning.

2. **Operational failures** (`Error`, `ProviderUnavailable`, `InsufficientData`): The check could not be completed due to a provider issue, missing data, or internal error. These are not compliance clearance.

3. **Stale evidence** (`Expired`): A previously-approved decision has exceeded its validity window. Rescreening is required.

Operators and downstream surfaces **must not** treat operational errors or stale evidence as compliance clearance. The service is fail-closed: `Error`, `ProviderUnavailable`, `InsufficientData`, and `Expired` states all block downstream approval.

### Remediation Guidance by State

| State | Meaning | Operator Action |
|---|---|---|
| `ProviderUnavailable` | Screening provider unreachable | Wait for provider recovery; retry screening |
| `InsufficientData` | Required subject metadata missing | Collect additional subject information and resubmit |
| `Expired` | Evidence validity window elapsed | Resubmit a new screening request for the subject |
| `Error` (Timeout) | Provider took too long | Retry; check provider status page |
| `Error` (MalformedResponse) | Unparseable provider response | Check provider API version compatibility |
| `Error` (InternalError) | Unexpected service error | Check application logs; contact support |

---

## Testing

### Unit Tests

File: `BiatecTokensTests/ComplianceOrchestrationServiceTests.cs`

Covers:
- Input validation (missing SubjectId, ContextId)
- KYC-only, AML-only, and Combined check paths
- All provider outcomes (Approved, Rejected, NeedsReview, Error, Timeout, ProviderUnavailable, MalformedResponse)
- Combined check priority rules (KYC hard-failure skips AML)
- Idempotency and replay semantics (3-replay determinism)
- Audit trail correctness
- Reviewer notes: append, persist, appear in status/history, evidence references, unique IDs
- Fail-closed behavior for provider crashes

File: `BiatecTokensTests/ComplianceOrchestrationEnhancementsTests.cs`

Covers:
- `ProviderUnavailable` state as a distinct first-class state (not Error)
- `InsufficientData` state — fail-closed when required data is missing
- Evidence freshness / `Expired` state via `evidenceValidityHours` and `evidenceExpiresAt`
- `ScreeningSubjectType` (Individual vs BusinessEntity) preserved in responses and history
- `matchedWatchlistCategories` populated for sanctions and PEP hits
- `matchedWatchlistCategories` preserved in status retrieval
- Combined-check fail-fast on `ProviderUnavailable` / `InsufficientData`
- `ComplianceDecisionState` enum completeness assertions

### Integration Tests

File: `BiatecTokensTests/ComplianceOrchestrationIntegrationTests.cs`

Covers:
- Full HTTP pipeline for all 4 endpoints
- Authentication enforcement (401 on unauthenticated requests)
- State contract (response shape, required fields)
- Idempotency through HTTP
- Decision history isolation across subjects
- Reviewer note HTTP lifecycle (append, retrieve in status, multiple notes, evidence references, 404, 400, 401)
- Correlation ID propagation through HTTP headers

### Running Tests

```bash
# Unit tests only
dotnet test --filter "FullyQualifiedName~ComplianceOrchestrationServiceTests" --configuration Release

# Enhancement tests only
dotnet test --filter "FullyQualifiedName~ComplianceOrchestrationEnhancementsTests" --configuration Release

# Integration tests only
dotnet test --filter "FullyQualifiedName~ComplianceOrchestrationIntegrationTests" --configuration Release

# All compliance tests
dotnet test --filter "FullyQualifiedName~ComplianceOrchestration" --configuration Release
```

---

## Manual Verification

### 1. Create a Screening Case

```bash
curl -X POST https://localhost:7000/api/v1/compliance-orchestration/initiate \
  -H "Authorization: Bearer <jwt>" \
  -H "Content-Type: application/json" \
  -d '{
    "subjectId": "issuer-org-001",
    "contextId": "issuance-2026-token-A",
    "checkType": 2,
    "subjectMetadata": {
      "full_name": "Acme Corp Ltd",
      "country": "DE"
    }
  }'
```

### 2. Retrieve Case Status

```bash
curl https://localhost:7000/api/v1/compliance-orchestration/status/<decisionId> \
  -H "Authorization: Bearer <jwt>"
```

### 3. Append a Reviewer Note

```bash
curl -X POST https://localhost:7000/api/v1/compliance-orchestration/notes/<decisionId> \
  -H "Authorization: Bearer <jwt>" \
  -H "Content-Type: application/json" \
  -d '{
    "content": "Corporate registration documents verified. Beneficial owner confirmed.",
    "evidenceReferences": {
      "registration_doc": "doc-reg-001",
      "beneficial_owner_form": "doc-bo-002"
    }
  }'
```

### 4. Simulate a Provider Failure

```bash
curl -X POST https://localhost:7000/api/v1/compliance-orchestration/initiate \
  -H "Authorization: Bearer <jwt>" \
  -H "Content-Type: application/json" \
  -d '{
    "subjectId": "test-error-subject",
    "contextId": "ctx-error-test",
    "checkType": 1,
    "subjectMetadata": { "simulate_timeout": "true" }
  }'
# Expected: state=4 (Error), providerErrorCode=1 (Timeout)
```

### 5. Verify Failure vs Adverse Outcome Are Distinguishable

Operational error → `state: 4` (Error), `providerErrorCode: 1|2|3|4|5`
Adverse outcome → `state: 2` (Rejected), `providerErrorCode: 0`

---

## Roadmap Alignment

This layer advances the following Biatec Tokens roadmap areas:

| Roadmap Area | How This Work Contributes |
|---|---|
| **KYC Integration** | Establishes stable `IKycProvider` contract and mock adapter; ready for live vendor binding |
| **AML Screening** | Establishes stable `IAmlProvider` contract and mock adapter with all outcome types |
| **Advanced MICA Compliance** | Decision audit trail and reviewer notes support regulator-facing evidence export |
| **Enterprise Dashboard** | Normalized `ComplianceDecisionState` and `ReviewerNote` types support operator UX surfaces |
| **Regulatory Integration** | `ComplianceAuditEvent` records provide the event log needed for future reporting exports |

The design intentionally avoids vendor lock-in. Provider-specific mapping is isolated behind `IKycProvider` and `IAmlProvider`. The broader product remains vendor-agnostic until a commercial decision is made about live integration partners.

---

## Security Notes

- All endpoints require JWT Bearer authentication (`[Authorize]`).
- User inputs are sanitized before logging using `LoggingHelper.SanitizeLogInput()` to prevent log injection attacks.
- Provider credentials must not be committed to source; use environment variables or secrets management.
- The service is fail-closed: unhandled provider exceptions result in `Error` state, never silent success.

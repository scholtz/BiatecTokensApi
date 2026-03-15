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

### POST `/rescreen/{decisionId}`

Initiates a rescreen for a subject whose evidence is stale or expired.
A new compliance decision is created using the same subject and context as the original,
with optional parameter overrides. The original decision is not modified, but receives a
`RescreenTriggered` audit event for traceability.

**Authentication:** Required (Bearer JWT).

**Path parameter:**
| Parameter | Description |
|---|---|
| `decisionId` | The original compliance decision ID to rescreen |

**Request body (optional):**
```json
{
  "checkType": 2,
  "subjectMetadata": {
    "full_name": "Jane Doe",
    "country": "DE"
  },
  "evidenceValidityHours": 720,
  "reason": "EvidenceExpired"
}
```

| Field | Type | Required | Description |
|---|---|---|---|
| `checkType` | int | No | Override the check type. When omitted, the original check type is reused |
| `subjectMetadata` | dict | No | Updated subject metadata. When omitted, empty metadata is used |
| `evidenceValidityHours` | int | No | Override the evidence validity window. When omitted, no expiry is set unless specified |
| `reason` | string | No | Human-readable reason for the rescreen (default: `"OperatorRequested"`) |

**Response (200 OK):**
```json
{
  "success": true,
  "previousDecisionId": "a1b2c3d4e5f6...",
  "newDecision": {
    "success": true,
    "decisionId": "x7y8z9...",
    "state": 1,
    "auditTrail": [ ... ],
    ...
  },
  "correlationId": "corr-xyz"
}
```

**Error Response (404 Not Found):** Original decision not found.

**Error Response (400 Bad Request):** Missing or empty decision ID.

**Typical use cases:**
- Subject's evidence expired (`state = 6 / Expired`) — trigger fresh KYC/AML checks
- Operator manually initiates a periodic re-check regardless of expiry
- Compliance policy change requires all subjects to be re-screened

---

### POST `/provider-callback`

Processes an inbound provider webhook/callback event and updates the corresponding compliance decision.
This endpoint is **anonymous** (no Bearer JWT required) — providers POST directly to it.
Authenticity is validated via the `signature` field in the request body.

Duplicate callbacks with the same `idempotencyKey` are accepted without re-processing
(idempotent replay detection). The `isIdempotentReplay: true` flag signals this to the caller.

**Request:**
```json
{
  "providerName": "StripeIdentity",
  "providerReferenceId": "vs_1234567890",
  "eventType": "identity.verification_session.verified",
  "outcomeStatus": "approved",
  "reasonCode": null,
  "message": "Document and selfie passed all checks.",
  "signature": "sha256=abcdef...",
  "idempotencyKey": "evt_stripe_001"
}
```

| Field | Type | Required | Description |
|---|---|---|---|
| `providerName` | string | No | Provider identifier (e.g. `"StripeIdentity"`, `"ComplyAdvantage"`, `"Mock"`) |
| `providerReferenceId` | string | ✅ | Provider-issued reference ID matching `kycProviderReferenceId` or `amlProviderReferenceId` on the decision |
| `eventType` | string | No | Provider-specific event type string |
| `outcomeStatus` | string | ✅ | Normalised outcome: `approved`, `rejected`, `needs_review`, `pending`, `provider_unavailable`, `insufficient_data`, `expired` (unrecognised values → `Error` / fail-closed) |
| `reasonCode` | string | No | Optional reason code (e.g. `SANCTIONS_MATCH`) |
| `message` | string | No | Optional human-readable description from the provider |
| `signature` | string | No | HMAC-SHA256 signature for authenticity validation |
| `idempotencyKey` | string | No | Idempotency key for the event — duplicate calls with the same key are not re-processed |

**Response (200 OK):**
```json
{
  "success": true,
  "decisionId": "a1b2c3d4e5f6...",
  "newState": 1,
  "isIdempotentReplay": false,
  "correlationId": "corr-xyz"
}
```

**Response (200 OK — idempotent replay):**
```json
{
  "success": true,
  "isIdempotentReplay": true,
  "correlationId": "corr-xyz"
}
```

**Error Response (404 Not Found):** No decision found for the given `providerReferenceId`.

**Error Response (400 Bad Request):** Missing required fields.

**Outcome Status Mapping:**

| `outcomeStatus` value | Mapped `ComplianceDecisionState` |
|---|---|
| `approved`, `verified`, `passed`, `clear` | `1 (Approved)` |
| `rejected`, `failed`, `declined`, `blocked` | `2 (Rejected)` |
| `needs_review`, `needsreview`, `review`, `manual_review` | `3 (NeedsReview)` |
| `pending`, `processing`, `in_progress` | `0 (Pending)` |
| `provider_unavailable`, `unavailable`, `offline` | `5 (ProviderUnavailable)` |
| `insufficient_data`, `insufficientdata`, `incomplete` | `7 (InsufficientData)` |
| `expired`, `stale` | `6 (Expired)` |
| *(any unrecognised value)* | `4 (Error)` — fail-closed |

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

---

## Production Provider Adapters

### Overview

The orchestration layer now includes two production-oriented provider adapters that can be activated through configuration. Both implement the existing `IKycProvider` / `IAmlProvider` interfaces and are registered via factory delegation in `Program.cs`. No code changes are required to switch providers—only configuration changes.

**Provider selection is configuration-driven and fail-closed:** if a required setting (such as an API key) is missing or blank, the provider returns `ProviderUnavailable` or `NotStarted` rather than silently allowing subjects through.

---

### KYC: `StripeIdentityKycProvider`

A production-oriented adapter for the [Stripe Identity](https://stripe.com/docs/identity) API. It creates verification sessions for both individual and business-entity subjects and maps Stripe event types and session statuses to normalized `KycStatus` values.

#### Activation

```json
// appsettings.json (non-production placeholder only)
"KycConfig": {
  "Provider": "StripeIdentity",
  "ApiEndpoint": "https://api.stripe.com",
  "RequestTimeoutSeconds": 30
}
```

**Inject the secret via environment variable (never commit to source):**

```bash
export KycConfig__ApiKey="sk_live_..."
export KycConfig__WebhookSecret="whsec_..."
```

#### Subject Metadata

| Metadata Key | Individual | BusinessEntity |
|---|---|---|
| `subject_type` | `"Individual"` | `"BusinessEntity"` |
| `full_name` | ✅ | — |
| `legal_name` | — | ✅ |
| `registration_number` | — | Optional |
| `jurisdiction` | — | Optional |

#### Status Mapping

| Stripe Session Status | Normalized `KycStatus` |
|---|---|
| `verified` | `Approved` |
| `requires_input` | `NeedsReview` |
| `processing` | `Pending` |
| `canceled` | `Rejected` |
| `expired` | `Expired` |
| Unknown | `Pending` |

#### Webhook Event Mapping

| Stripe Event Type | Normalized `KycStatus` |
|---|---|
| `identity.verification_session.verified` | `Approved` |
| `identity.verification_session.requires_input` | `NeedsReview` |
| `identity.verification_session.canceled` | `Rejected` |
| `identity.verification_session.processing` | `Pending` |

Webhook signatures use Stripe's HMAC-SHA256 format (`t=<timestamp>,v1=<hash>`). Validate with `ValidateWebhookSignature(payload, header, webhookSecret)`.

#### Fail-Closed Behaviour

- Missing `ApiKey` → all calls return `KycStatus.NotStarted` with explanatory error message.
- Network error → `KycStatus.NotStarted` + `"Network error communicating with KYC provider"`.
- Timeout → `KycStatus.NotStarted` + `"Provider request timed out"`.
- Non-2xx response → `KycStatus.NotStarted` + `"Provider error {status}: {message}"`.
- Malformed JSON response → `KycStatus.NotStarted` + `"Malformed provider response"`.

---

### AML: `ComplyAdvantageAmlProvider`

A production-oriented adapter for the [ComplyAdvantage](https://complyadvantage.com) Screening API. It screens subjects against sanctions lists, PEP watchlists, and (optionally) adverse media sources, and maps ComplyAdvantage match results to normalized `ComplianceDecisionState` values.

#### Activation

```json
// appsettings.json (non-production placeholder only)
"AmlConfig": {
  "Provider": "ComplyAdvantage",
  "ApiEndpoint": "https://api.complyadvantage.com",
  "IncludePepScreening": true,
  "IncludeAdverseMedia": false,
  "FuzzinessThreshold": 0,
  "MinApprovalConfidence": 0.8,
  "EvidenceValidityHours": 720,
  "RequestTimeoutSeconds": 30
}
```

**Inject the secret via environment variable:**

```bash
export AmlConfig__ApiKey="..."
```

#### AmlConfig Fields

| Field | Type | Default | Description |
|---|---|---|---|
| `Provider` | string | `"Mock"` | `"Mock"` or `"ComplyAdvantage"` |
| `EnforcementEnabled` | bool | `false` | When false, results are advisory only |
| `ApiEndpoint` | string | `https://api.complyadvantage.com` | Base URL |
| `ApiKey` | string | — | **Required for production.** Inject via env var |
| `WebhookSecret` | string | — | For validating async callbacks |
| `EvidenceValidityHours` | int | `720` | Hours before an approved decision expires |
| `MaxRetryAttempts` | int | `3` | Retries for transient failures |
| `RetryDelayMs` | int | `1000` | Initial retry delay (exponential backoff) |
| `RequestTimeoutSeconds` | int | `30` | Per-request timeout |
| `MinApprovalConfidence` | decimal | `0.8` | Below this threshold, clean results → NeedsReview |
| `IncludePepScreening` | bool | `true` | Include PEP lists in every search |
| `IncludeAdverseMedia` | bool | `false` | Include adverse media sources |
| `FuzzinessThreshold` | int | `0` | Name-match fuzziness (0=exact, 100=most lenient) |

#### Decision Mapping

| ComplyAdvantage Result | Normalized State |
|---|---|
| Zero hits | `Approved` |
| Sanctions hit (any OFAC/EU/UN/generic) | `Rejected` + `SANCTIONS_MATCH` |
| PEP hit | `NeedsReview` + `PEP_MATCH` |
| Adverse media hit | `NeedsReview` + `ADVERSE_MEDIA_MATCH` |
| Other hit types | `NeedsReview` + `REVIEW_REQUIRED` |
| Sanctions + PEP | `Rejected` (sanctions take priority) |
| HTTP 503 / 429 | `ProviderUnavailable` |
| HTTP 400 / 401 | `Error` |
| Malformed JSON | `Error` + `MALFORMED_RESPONSE` |
| Network error | `ProviderUnavailable` + `PROVIDER_NETWORK_ERROR` |
| Timeout | `ProviderUnavailable` + `PROVIDER_TIMEOUT` |

#### Business Entity Screening

For `subjectType = BusinessEntity`, the adapter uses `legal_name` as the search term (falling back to `subjectId` if absent) and sets `entity_type = "company"` in the provider request. Include these metadata keys:

```json
"subjectMetadata": {
  "subject_type": "BusinessEntity",
  "legal_name": "Acme Corp Ltd",
  "registration_number": "12345678",
  "jurisdiction": "UK"
}
```

#### Fail-Closed Behaviour

- Missing `ApiKey` → `ProviderUnavailable` + `PROVIDER_NOT_CONFIGURED`.
- 503 / 429 responses → `ProviderUnavailable` (transient, retryable).
- 400 / 401 responses → `Error` (terminal, not retryable).
- Network error → `ProviderUnavailable` + `PROVIDER_NETWORK_ERROR`.
- Timeout → `ProviderUnavailable` + `PROVIDER_TIMEOUT`.

---

### Configuration-Driven Provider Selection

Provider selection happens automatically in `Program.cs` based on the `Provider` field in each config section. Unknown provider names silently fall back to `Mock`.

```csharp
// KYC provider selection (Program.cs)
services.AddSingleton<IKycProvider>(sp => {
    var cfg = sp.GetRequiredService<IOptions<KycConfig>>().Value;
    return cfg.Provider == "StripeIdentity"
        ? sp.GetRequiredService<StripeIdentityKycProvider>()
        : sp.GetRequiredService<MockKycProvider>();
});

// AML provider selection (Program.cs)
services.AddSingleton<IAmlProvider>(sp => {
    var cfg = sp.GetRequiredService<IOptions<AmlConfig>>().Value;
    return cfg.Provider == "ComplyAdvantage"
        ? sp.GetRequiredService<ComplyAdvantageAmlProvider>()
        : sp.GetRequiredService<MockAmlProvider>();
});
```

---

### Testing the Production Adapters

Run provider adapter tests (no real network calls):

```bash
# KYC adapter tests (StripeIdentityKycProvider)
dotnet test --filter "FullyQualifiedName~KycProviderAdapterTests" --configuration Release

# AML adapter tests (ComplyAdvantageAmlProvider)
dotnet test --filter "FullyQualifiedName~AmlProviderAdapterTests" --configuration Release

# Integration tests (provider-driven orchestration scenarios)
dotnet test --filter "FullyQualifiedName~ComplianceProviderIntegrationTests" --configuration Release

# All compliance provider tests
dotnet test --filter "FullyQualifiedName~KycProviderAdapterTests|FullyQualifiedName~AmlProviderAdapterTests|FullyQualifiedName~ComplianceProviderIntegrationTests" --configuration Release
```

**Expected output:** 81 tests passing, 0 failing.

---

### Validating Production Configuration

Before enabling production providers, verify configuration is complete:

```bash
# Check for missing keys (should print warning in startup logs if ApiKey is missing)
dotnet run --project BiatecTokensApi -- --environment Development 2>&1 | grep -i "not configured\|ApiKey"

# Smoke-test the mock path first
curl -X POST https://localhost:7000/api/v1/compliance-orchestration/initiate \
  -H "Authorization: Bearer <jwt>" \
  -H "Content-Type: application/json" \
  -d '{"subjectId":"smoke-test-001","contextId":"smoke-ctx-001","checkType":1}'
# Expected: success=true, state reflects MockAmlProvider behavior
```

**Before going live:**

1. Set `KycConfig__ApiKey` and `AmlConfig__ApiKey` environment variables in your deployment environment.
2. Set `KycConfig__WebhookSecret` for Stripe webhook validation.
3. Set `KycConfig__Provider=StripeIdentity` and `AmlConfig__Provider=ComplyAdvantage`.
4. Test with a known clean subject to confirm Approved path.
5. Test with a known sanctions-listed subject (Stripe Identity provides test IDs for this).
6. Monitor logs for any `"not configured"` or `"ProviderUnavailable"` messages.

---

### Failure Mode Reference

| Scenario | KYC Outcome | AML Outcome | Orchestration Decision |
|---|---|---|---|
| Both providers pass | `Approved` | `Approved` | `Approved` |
| KYC passes, AML sanctions hit | `Approved` | `Rejected` | `Rejected` |
| KYC passes, AML PEP match | `Approved` | `NeedsReview` | `NeedsReview` |
| KYC passes, AML unavailable | `Approved` | `ProviderUnavailable` | `ProviderUnavailable` |
| KYC rejected | `Rejected` | (skipped) | `Rejected` |
| KYC provider unavailable | `NotStarted` → mapped to `ProviderUnavailable` | (skipped) | `ProviderUnavailable` |
| Both providers unavailable | `ProviderUnavailable` | `ProviderUnavailable` | `ProviderUnavailable` |
| Missing KYC API key | `NotStarted` | — | `Error` (KYC_PROVIDER_ERROR) |
| Missing AML API key | — | `ProviderUnavailable` | `ProviderUnavailable` |

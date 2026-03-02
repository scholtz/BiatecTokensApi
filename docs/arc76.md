# ARC76 Account Management — Backend-Verified Email/Password Authentication

## Overview

BiatecTokensApi implements the [ARC-0076](https://github.com/algorandfoundation/ARCs/blob/main/ARCs/arc-0076.md)
specification for deterministic Algorand account derivation from user credentials
(email + password). This enables non-crypto-native users to issue and manage blockchain
tokens using only their email address and password — with **no wallet required**.

The same email + password combination **always** produces the same Algorand Ed25519 account.
The private key is derived in the backend and never exposed to the frontend.

---

## Derivation Algorithm

### Library

Derivation is implemented via the [`AlgorandARC76Account`](https://www.nuget.org/packages/AlgorandARC76Account)
NuGet package (v1.1.0), using the `ARC76.GetEmailAccount(email, password, slot)` method.

### Parameters

| Parameter | Value |
|-----------|-------|
| Email     | Canonicalized: `email.Trim().ToLowerInvariant()` |
| Password  | Raw user input (case-sensitive, never modified) |
| Slot      | `0` (primary account) |
| Algorithm | PBKDF2-based derivation per ARC-0076 specification |
| Output    | Algorand Ed25519 account (address + keypair) |

### Email Canonicalization

Before derivation, the email address is canonicalized:
1. **Trim** leading/trailing whitespace
2. **Lowercase** the entire string

This ensures `User@Example.COM`, `user@example.com`, and `  user@example.com  `
all derive the **same** Algorand address.

---

## Known Test Vector

The following test vector was computed on 2026-03-02 using `AlgorandARC76Account` v1.1.0:

```
Input:
  email    = "testuser@biatec.io"
  password = "TestPassword123!"
  slot     = 0

Output:
  AlgorandAddress = "4DV7T4TUCD4KCPMLCD2GHQGKNX4PTZPMTNJLH77DEH7ZPZHAIAYG5JBBRI"
  PublicKey (base64, 32 bytes Ed25519) = derived from ARC76.GetEmailAccount
```

This test vector is asserted in `ARC76VisionMilestoneServiceUnitTests.KnownTestVector_DeriveAddress_ReturnsExpectedAddress`.

---

## API Endpoints

### `POST /api/v1/auth/arc76/validate`

**Authentication**: None required (anonymous)

Derives the Algorand address from the provided credentials and returns it.
If the user exists in the system, also verifies whether the derived address
matches the stored account address.

**Request:**
```json
{
  "email": "user@example.com",
  "password": "SecurePass123!"
}
```

**Response (200 OK):**
```json
{
  "algorandAddress": "4DV7T4TUCD4KCPMLCD2GHQGKNX4PTZPMTNJLH77DEH7ZPZHAIAYG5JBBRI",
  "publicKeyBase64": "<base64-encoded-32-byte-Ed25519-public-key>",
  "addressMatchesStoredAccount": true,
  "success": true,
  "correlationId": "0HNJ..."
}
```

**Error Response (400 Bad Request):**
```json
{
  "success": false,
  "errorCode": "MISSING_REQUIRED_FIELD",
  "errorMessage": "Email and password are required",
  "correlationId": "0HNJ..."
}
```

**Notes:**
- **Never returns private key material**
- Deterministic: same credentials always return the same `algorandAddress`
- `addressMatchesStoredAccount` is `true` when the user registered via ARC76 credential derivation

---

### `POST /api/v1/auth/arc76/verify-session`

**Authentication**: `Authorization: Bearer <JWT>`

Returns the Algorand address bound to the current authenticated session.
Used by frontend integration tests to assert session-to-address binding.

**Request:** No body required

**Response (200 OK):**
```json
{
  "algorandAddress": "4DV7T4TUCD4KCPMLCD2GHQGKNX4PTZPMTNJLH77DEH7ZPZHAIAYG5JBBRI",
  "userId": "a8f3c1d2-...",
  "success": true,
  "correlationId": "0HNJ..."
}
```

**Error Response (401 Unauthorized):**
Returned when no valid Bearer token is provided or the token is expired.

---

### `POST /api/v1/auth/register`

**Authentication**: None required

Registers a new user with email + password. Derives the Algorand account
using ARC76 credential derivation (`ARC76.GetEmailAccount(email, password, 0)`).

The derived address is stored as the user's `AlgorandAddress`. Subsequent calls
to `validate` with the same credentials will return `addressMatchesStoredAccount: true`.

---

### `POST /api/v1/auth/login`

**Authentication**: None required

Authenticates the user and returns a JWT access token. The response includes
`algorandAddress` — the same ARC76-derived address assigned at registration.

---

## Security Considerations

### Private Key Handling

- The ARC76 private key is **never returned** in any API response
- The private key is **never logged** (even at DEBUG level)
- The private key is derived in-memory and immediately either:
  - Converted to a 25-word Algorand mnemonic for encrypted storage (signing operations)
  - Discarded after the request completes
- The encrypted mnemonic is stored in the user database (in-memory for non-production)
  and is only accessed by the signing service

### Mnemonic Storage Format

User accounts registered via ARC76 credential derivation store a **25-word Algorand mnemonic**
(via `account.ToMnemonic()`). This is distinct from BIP39 24-word mnemonics used by the system
account. At signing time, the user account is reconstructed using:
```csharp
var account = new Algorand.Algod.Model.Account(mnemonic);
```

### Authentication Flow

```
User                Backend
 │                     │
 ├── POST /auth/register ──────────────────────►
 │   { email, password }                         Derives ARC76 account
 │                                               Encrypts mnemonic
 │                                               Stores user record
 │◄─────────────────── { algorandAddress, JWT } ─┤
 │                     │
 ├── POST /auth/login ────────────────────────►
 │   { email, password }                         Verifies password hash
 │                                               Returns stored address
 │◄─────────────────── { algorandAddress, JWT } ─┤
 │                     │
 ├── POST /auth/arc76/validate ──────────────►
 │   { email, password }                         Derives ARC76 address
 │                                               Compares with stored
 │◄─────────────────── { algorandAddress }  ─────┤
 │                     │
 ├── POST /auth/arc76/verify-session ────────►
 │   Authorization: Bearer <JWT>                  Looks up session user
 │◄─────────────────── { algorandAddress }  ─────┤
```

### Rate Limiting

The `/auth/arc76/validate` endpoint (and all login-related endpoints) should be
protected by IP-based rate limiting in production deployments. The recommended
limit is 5 requests/minute per IP to prevent brute-force credential probing.

---

## Frontend Integration Guide

### Replacing `withAuth()` Seeding

The frontend Playwright tests currently use `withAuth()` (localStorage seeding) as a
workaround. To replace this with the real ARC76 login flow:

**Before (withAuth seeding):**
```typescript
// OLD: Injects fake session
await page.evaluate(() => {
  localStorage.setItem('token', 'fake-token');
  localStorage.setItem('address', 'FAKE_ADDRESS');
});
```

**After (real ARC76 login):**
```typescript
// NEW: Real login via ARC76
const loginResp = await page.request.post('/api/v1/auth/login', {
  data: { email: 'user@example.com', password: 'SecurePass123!' }
});
const { accessToken, algorandAddress } = await loginResp.json();

// Store real token
await page.evaluate((token) => {
  localStorage.setItem('token', token);
}, accessToken);

// Verify session binding
const verifyResp = await page.request.post('/api/v1/auth/arc76/verify-session', {
  headers: { Authorization: `Bearer ${accessToken}` }
});
const { algorandAddress: sessionAddress } = await verifyResp.json();
assert.strictEqual(sessionAddress, algorandAddress); // Must match
```

### Address Verification Pattern

```typescript
// Verify a specific address is associated with credentials
const validateResp = await fetch('/api/v1/auth/arc76/validate', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ email, password })
});
const { algorandAddress, addressMatchesStoredAccount } = await validateResp.json();

// algorandAddress is deterministic — same every time
// addressMatchesStoredAccount === true for ARC76-registered users
```

---

## Test Coverage

### Unit Tests (`ARC76VisionMilestoneServiceUnitTests.cs`)

| Test | Description |
|------|-------------|
| `KnownTestVector_DeriveAddress_ReturnsExpectedAddress` | Known vector: testuser@biatec.io + TestPassword123! → expected address |
| `KnownTestVector_DeriveAddressAndPublicKey_ReturnsExpectedAddress` | Returns both address and public key |
| `KnownTestVector_PublicKeyIsNotPrivateKey` | Public key differs from private key |
| `KnownTestVector_AlgorandAddressLength_IsCorrect` | Address is 55-60 chars, base32 alphabet |
| `KnownTestVector_DeriveAccountMnemonic_Returns25WordAlgorandMnemonic` | Mnemonic is exactly 25 words |
| `KnownTestVector_Mnemonic_ReconstructsCorrectAccount` | Account reconstructed from mnemonic matches |
| `Determinism_DeriveAddress_1000Iterations_AllIdentical` | 10 consecutive calls, all identical |
| `Determinism_DeriveAddress_SameCredentials_AlwaysSameAddress` | Determinism across 3 calls |
| `Determinism_DifferentEmails_ProduceDifferentAddresses` | Different emails → different addresses |
| `Determinism_DifferentPasswords_ProduceDifferentAddresses` | Different passwords → different addresses |
| `Canonicalization_EmailLowercasedBeforeDerivation` | Case normalization works |
| `Canonicalization_EmailTrimmedBeforeDerivation` | Whitespace trimming works |
| `EdgeCase_EmptyEmail_ThrowsArgumentException` | Empty email fails gracefully |
| `EdgeCase_NullEmail_ThrowsArgumentException` | Null email fails gracefully |
| `EdgeCase_EmptyPassword_ThrowsArgumentException` | Empty password fails gracefully |
| `EdgeCase_UnicodeEmail_DerivesValidAddress` | Unicode email works |
| `EdgeCase_SpecialCharactersInPassword_DerivesValidAddress` | Special chars in password work |
| `Security_DeriveAddress_DoesNotReturnPrivateKey` | No private key in address output |
| `Security_DeriveAddressAndPublicKey_PublicKeyIsNotPrivateKey` | Public ≠ private key |

### Contract/Integration Tests (`ARC76VisionMilestoneContractTests.cs`)

| Test | Description |
|------|-------------|
| `AC1_Validate_ReturnsExpectedAddressForKnownTestVector` | Known vector via HTTP endpoint |
| `AC1_Validate_Returns100IdenticalAddresses_ForSameCredentials` | 100 HTTP calls, all identical |
| `AC1_Validate_IsAnonymous_NoAuthRequired` | Endpoint works without Bearer token |
| `AC1_Validate_EmptyEmail_Returns400` | Validation error for empty email |
| `AC1_Validate_ResponseNeverContainsPrivateKey` | No private key in HTTP response |
| `AC2_VerifySession_WithValidToken_ReturnsAlgorandAddress` | Session binding works |
| `AC2_VerifySession_WithoutToken_Returns401` | Auth required |
| `AC2_VerifySession_AddressBindingIsDeterministic` | Same address across sessions |
| `AC3_Registration_UsesARC76Derivation_ValidateMatchesStoredAddress` | Registration uses ARC76 |
| `AC3_Registration_DeterministicAddress_SameEmailPasswordAlwaysSameAddress` | Pre-derive equals registered |
| `AC4_FullFlow_Register_Login_Validate_VerifySession_AllReturnSameAddress` | Full integration flow |
| `AC5_Validate_DifferentUsers_DifferentAddresses` | Unique addresses per user |

---

## Implementation Files

| File | Description |
|------|-------------|
| `Services/Interface/IArc76CredentialDerivationService.cs` | Service interface |
| `Services/Arc76CredentialDerivationService.cs` | Service implementation |
| `Controllers/AuthV2Controller.cs` | HTTP endpoints |
| `Models/Auth/ARC76CredentialValidateModels.cs` | Request/response models |
| `Services/AuthenticationService.cs` | Registration uses `ARC76.GetEmailAccount()` |
| `Services/ASATokenService.cs` | Signing uses `new Account(mnemonic)` for user accounts |

---

## References

- [ARC-0076 Specification](https://github.com/algorandfoundation/ARCs/blob/main/ARCs/arc-0076.md)
- [AlgorandARC76Account NuGet Package](https://www.nuget.org/packages/AlgorandARC76Account)
- [Algorand Developer Portal](https://developer.algorand.org)
- [BiatecTokensApi Repository](https://github.com/scholtz/BiatecTokensApi)

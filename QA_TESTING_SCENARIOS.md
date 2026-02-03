# QA Testing Scenarios - Backend Stability & Subscription APIs

## Overview

This document provides manual test scenarios for QA engineers to validate the backend integration stability, subscription APIs, and new features including error handling enhancements and idempotency support.

## Prerequisites

- API endpoint URL (e.g., https://api-staging.example.com)
- Test Algorand account with sufficient funds
- Stripe test account credentials
- API testing tool (Postman, curl, or similar)
- Test wallet for ARC-0014 authentication

---

## Test Suite 1: Error Handling & Remediation Hints

### Test 1.1: Validation Error with Remediation Hint

**Objective**: Verify that validation errors return helpful remediation hints

**Steps**:
1. Send POST request to `/api/v1/token/erc20-mintable/create` without authentication
2. Observe the error response

**Expected Result**:
```json
{
  "success": false,
  "errorCode": "UNAUTHORIZED",
  "errorMessage": "Authentication is required to access this resource",
  "remediationHint": "Provide a valid ARC-0014 authentication token in the Authorization header.",
  "timestamp": "...",
  "path": "/api/v1/token/erc20-mintable/create",
  "correlationId": "..."
}
```

**Pass Criteria**:
- ✅ Error code is `UNAUTHORIZED`
- ✅ `remediationHint` field is present and non-empty
- ✅ Hint provides actionable guidance
- ✅ Correlation ID is present for tracing

---

### Test 1.2: Blockchain Connection Error

**Objective**: Verify blockchain connection errors return helpful hints

**Steps**:
1. Configure API to point to invalid blockchain endpoint (or wait for network issue)
2. Attempt token deployment
3. Observe error response

**Expected Result**:
```json
{
  "success": false,
  "errorCode": "BLOCKCHAIN_CONNECTION_ERROR",
  "errorMessage": "Failed to connect to [network] blockchain network...",
  "remediationHint": "Check network status and availability. If the problem persists, contact support.",
  ...
}
```

**Pass Criteria**:
- ✅ Error code is `BLOCKCHAIN_CONNECTION_ERROR`
- ✅ Remediation hint suggests checking network status
- ✅ HTTP status code is 502 (Bad Gateway)

---

### Test 1.3: Timeout Error

**Objective**: Verify timeout errors include retry guidance

**Steps**:
1. Send a request that will timeout (adjust timeout settings if needed)
2. Observe error response

**Expected Result**:
```json
{
  "success": false,
  "errorCode": "TIMEOUT",
  "errorMessage": "The ... operation timed out. Please try again.",
  "remediationHint": "The operation took too long to complete. This may be temporary - please retry your request.",
  ...
}
```

**Pass Criteria**:
- ✅ Error code is `TIMEOUT`
- ✅ Remediation hint suggests retry
- ✅ HTTP status code is 408 (Request Timeout)

---

## Test Suite 2: Idempotency Support

### Test 2.1: Successful Idempotency (Cache Hit)

**Objective**: Verify idempotency prevents duplicate deployments

**Steps**:
1. Generate a unique UUID for idempotency key: `test-deploy-12345`
2. Send POST request to `/api/v1/token/erc20-mintable/create` with:
   - Valid authentication
   - Valid token parameters
   - Header: `Idempotency-Key: test-deploy-12345`
3. Wait for response
4. Send the EXACT same request again with the same idempotency key
5. Compare responses

**Expected Result**:
- First request: Returns deployment response, header `X-Idempotency-Hit: false`
- Second request: Returns SAME response, header `X-Idempotency-Hit: true`

**Pass Criteria**:
- ✅ First request succeeds
- ✅ Second request returns identical response (same deploymentId, assetId, etc.)
- ✅ `X-Idempotency-Hit` header is `false` on first request
- ✅ `X-Idempotency-Hit` header is `true` on second request
- ✅ Only ONE token is actually deployed (verify on blockchain)

**Sample Request**:
```bash
curl -X POST https://api-staging.example.com/api/v1/token/erc20-mintable/create \
  -H "Authorization: SigTx <base64-signed-tx>" \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: test-deploy-12345" \
  -d '{
    "name": "Test Token",
    "symbol": "TEST",
    "decimals": 18,
    "initialSupply": "1000000",
    "network": "testnet"
  }'
```

---

### Test 2.2: Idempotency Across All Deployment Endpoints

**Objective**: Verify idempotency works for all 11 deployment endpoints

**Endpoints to Test**:
1. `/api/v1/token/erc20-mintable/create`
2. `/api/v1/token/erc20-preminted/create`
3. `/api/v1/token/asa-ft/create`
4. `/api/v1/token/asa-nft/create`
5. `/api/v1/token/asa-fnft/create`
6. `/api/v1/token/arc3-ft/create`
7. `/api/v1/token/arc3-nft/create`
8. `/api/v1/token/arc3-fnft/create`
9. `/api/v1/token/arc200-mintable/create`
10. `/api/v1/token/arc200-preminted/create`
11. `/api/v1/token/arc1400-mintable/create`

**Steps**: For EACH endpoint:
1. Generate unique idempotency key
2. Send request with idempotency key
3. Send duplicate request with same key
4. Verify cache hit

**Pass Criteria** (for each endpoint):
- ✅ Idempotency-Key header is accepted
- ✅ Duplicate request returns cached response
- ✅ X-Idempotency-Hit header indicates cache hit

---

### Test 2.3: Idempotency Expiration

**Objective**: Verify idempotency cache expires after 24 hours

**Steps**:
1. Send request with idempotency key
2. Modify system time to 25 hours in future (or wait if testing in real-time)
3. Send same request with same key
4. Observe response

**Expected Result**:
- Request executes as new (not cached)
- New deployment is created
- `X-Idempotency-Hit: false`

**Pass Criteria**:
- ✅ Expired cache entry is not returned
- ✅ New operation is performed
- ✅ Different deploymentId is returned

**Note**: This test may require special test environment setup to manipulate time.

---

### Test 2.4: Idempotency Without Key (Optional)

**Objective**: Verify API works normally when idempotency key is not provided

**Steps**:
1. Send POST request to deployment endpoint WITHOUT `Idempotency-Key` header
2. Send another request with different parameters, also without key
3. Observe responses

**Expected Result**:
- Both requests succeed normally
- Each creates a new deployment
- No `X-Idempotency-Hit` header in response

**Pass Criteria**:
- ✅ Requests succeed without idempotency key
- ✅ Each request creates separate deployment
- ✅ No caching behavior observed

---

## Test Suite 3: Subscription Management

### Test 3.1: Create Checkout Session

**Objective**: Verify Stripe checkout session creation

**Steps**:
1. Authenticate with valid ARC-0014 token
2. Send POST request to `/api/v1/subscription/checkout`:
```json
{
  "tier": "premium"
}
```
3. Observe response

**Expected Result**:
```json
{
  "success": true,
  "checkoutUrl": "https://checkout.stripe.com/...",
  "sessionId": "cs_test_..."
}
```

**Pass Criteria**:
- ✅ Response contains valid `checkoutUrl`
- ✅ Response contains `sessionId`
- ✅ Checkout URL redirects to Stripe payment page
- ✅ Stripe page shows correct tier and pricing

---

### Test 3.2: Get Subscription Status (Free Tier)

**Objective**: Verify subscription status for user without subscription

**Steps**:
1. Use test account that has never subscribed
2. Send GET request to `/api/v1/subscription/status`
3. Observe response

**Expected Result**:
```json
{
  "success": true,
  "subscription": {
    "userAddress": "...",
    "tier": "free",
    "status": "none",
    "cancelAtPeriodEnd": false,
    ...
  }
}
```

**Pass Criteria**:
- ✅ Tier is "free"
- ✅ Status is "none"
- ✅ No Stripe customer ID present

---

### Test 3.3: Complete Subscription Flow

**Objective**: End-to-end subscription activation

**Steps**:
1. Create checkout session for "basic" tier
2. Complete payment on Stripe (use test card: 4242 4242 4242 4242)
3. Wait for webhook to process (max 30 seconds)
4. Get subscription status
5. Verify tier and status updated

**Expected Result**:
```json
{
  "success": true,
  "subscription": {
    "tier": "basic",
    "status": "active",
    "stripeCustomerId": "cus_...",
    "stripeSubscriptionId": "sub_...",
    "currentPeriodStart": "...",
    "currentPeriodEnd": "...",
    ...
  }
}
```

**Pass Criteria**:
- ✅ Tier updated to "basic"
- ✅ Status is "active"
- ✅ Stripe IDs are present
- ✅ Period dates are valid

---

### Test 3.4: Billing Portal Access

**Objective**: Verify billing portal session creation

**Steps**:
1. Use authenticated user with active subscription
2. Send POST request to `/api/v1/subscription/billing-portal`
3. Access the portal URL
4. Verify portal functionality

**Expected Result**:
```json
{
  "success": true,
  "portalUrl": "https://billing.stripe.com/..."
}
```

**Pass Criteria**:
- ✅ Portal URL is returned
- ✅ Portal shows current subscription
- ✅ Can update payment method
- ✅ Can cancel subscription

---

### Test 3.5: Plan Limit Enforcement

**Objective**: Verify plan limits are enforced

**Steps**:
1. Get current usage: `GET /api/v1/billing/usage`
2. Note current token issuance count and limit
3. If below limit, deploy tokens until limit is reached
4. Attempt to deploy one more token
5. Observe error response

**Expected Result**:
```json
{
  "success": false,
  "errorCode": "SUBSCRIPTION_LIMIT_REACHED",
  "errorMessage": "You have reached your token deployment limit...",
  "remediationHint": "Upgrade your subscription plan to continue"
}
```

**Pass Criteria**:
- ✅ Error code is `SUBSCRIPTION_LIMIT_REACHED`
- ✅ HTTP status is 429 (Too Many Requests)
- ✅ Remediation hint suggests upgrade
- ✅ Operation is blocked

---

## Test Suite 4: Deployment Status Tracking

### Test 4.1: Real-time Status Updates

**Objective**: Verify deployment status transitions

**Steps**:
1. Deploy a token (any type)
2. Note the `deploymentId` from response
3. Poll status every 2 seconds: `GET /api/v1/token/deployments/{deploymentId}`
4. Record all status transitions
5. Verify final status

**Expected Status Flow**:
```
queued → submitted → pending → confirmed → completed
```

**Pass Criteria**:
- ✅ All expected statuses are observed
- ✅ Status transitions are in correct order
- ✅ Each status has timestamp
- ✅ Final status is "completed" or "failed"
- ✅ Asset ID is present on completion

---

### Test 4.2: Failed Deployment

**Objective**: Verify failed deployment provides diagnostic information

**Steps**:
1. Deploy token with insufficient funds (or invalid parameters)
2. Track deployment status
3. Observe failure details

**Expected Result**:
```json
{
  "deploymentId": "...",
  "currentStatus": "failed",
  "statusHistory": [
    {
      "status": "failed",
      "timestamp": "...",
      "message": "Insufficient funds",
      "errorDetails": { ... }
    }
  ]
}
```

**Pass Criteria**:
- ✅ Status is "failed"
- ✅ Error message is present
- ✅ Error details provide diagnostic info
- ✅ Status history shows failure transition

---

### Test 4.3: List Deployments with Filters

**Objective**: Verify deployment listing and filtering

**Steps**:
1. Create 5+ test deployments with different parameters
2. List all deployments: `GET /api/v1/token/deployments`
3. Filter by network: `GET /api/v1/token/deployments?network=testnet`
4. Filter by status: `GET /api/v1/token/deployments?status=completed`
5. Filter by date range
6. Test pagination

**Pass Criteria**:
- ✅ All deployments are listed
- ✅ Network filter works correctly
- ✅ Status filter works correctly
- ✅ Date range filter works correctly
- ✅ Pagination works (page, pageSize parameters)

---

## Test Suite 5: Health & Monitoring

### Test 5.1: Basic Health Check

**Objective**: Verify health check endpoint

**Steps**:
1. Send GET request to `/health`
2. Observe response

**Expected Result**:
- HTTP 200 OK
- Response body: "Healthy"

**Pass Criteria**:
- ✅ Returns 200 status code
- ✅ Response indicates healthy

---

### Test 5.2: Detailed Status Check

**Objective**: Verify detailed status endpoint

**Steps**:
1. Send GET request to `/api/v1/status`
2. Observe response structure

**Expected Result**:
```json
{
  "status": "Healthy",
  "version": "1.0.0",
  "buildTime": "...",
  "timestamp": "...",
  "uptime": "...",
  "environment": "Staging",
  "components": {
    "ipfs": {
      "status": "Healthy",
      "message": "..."
    },
    "algorand": {
      "status": "Healthy"
    },
    "evm": {
      "status": "Healthy"
    }
  }
}
```

**Pass Criteria**:
- ✅ All components show status
- ✅ Version information present
- ✅ Uptime is accurate
- ✅ Environment is correct

---

### Test 5.3: Degraded Service Detection

**Objective**: Verify health checks detect service issues

**Steps**:
1. Disable IPFS service (or simulate failure)
2. Check detailed status endpoint
3. Observe component status

**Expected Result**:
```json
{
  "status": "Degraded",
  "components": {
    "ipfs": {
      "status": "Unhealthy",
      "message": "Connection failed"
    }
  }
}
```

**Pass Criteria**:
- ✅ Overall status indicates degraded
- ✅ Affected component shows unhealthy
- ✅ Error message explains issue

---

## Test Suite 6: Audit Logging

### Test 6.1: Verify Audit Trail

**Objective**: Confirm critical actions are logged

**Steps**:
1. Perform token deployment
2. Query audit log: `GET /api/v1/enterprise-audit/audit-log`
3. Verify deployment is logged
4. Check log entry details

**Expected Result**:
```json
{
  "entries": [
    {
      "id": "...",
      "eventCategory": "TokenIssuance",
      "actionType": "Create",
      "performedBy": "...",
      "timestamp": "...",
      "assetId": "...",
      "network": "...",
      "successful": true,
      ...
    }
  ]
}
```

**Pass Criteria**:
- ✅ Deployment action is logged
- ✅ Log entry has all required fields
- ✅ Timestamp is accurate
- ✅ Performer address matches authenticated user

---

### Test 6.2: Audit Log Export

**Objective**: Verify audit log export functionality

**Steps**:
1. Export audit log as CSV: `GET /api/v1/enterprise-audit/audit-log/export/csv`
2. Export as JSON: `GET /api/v1/enterprise-audit/audit-log/export/json`
3. Verify exports contain data

**Pass Criteria**:
- ✅ CSV export downloads successfully
- ✅ JSON export downloads successfully
- ✅ Both formats contain same data
- ✅ Maximum 10,000 records per export

---

## Test Suite 7: Authentication

### Test 7.1: Valid ARC-0014 Authentication

**Objective**: Verify ARC-0014 authentication works

**Steps**:
1. Create auth transaction with note "BiatecTokens#ARC14"
2. Sign transaction with test account
3. Encode as base64
4. Send request with Authorization header: `SigTx <base64>`
5. Access protected endpoint

**Expected Result**:
- Request succeeds
- User identified by Algorand address

**Pass Criteria**:
- ✅ Authentication succeeds
- ✅ User address extracted from transaction
- ✅ Protected endpoint is accessible

---

### Test 7.2: Invalid Authentication

**Objective**: Verify invalid auth is rejected

**Steps**:
1. Send request with invalid Authorization header
2. Send request with expired transaction
3. Send request without Authorization header

**Expected Result** (for each):
```json
{
  "success": false,
  "errorCode": "UNAUTHORIZED",
  "remediationHint": "Provide a valid ARC-0014 authentication token..."
}
```

**Pass Criteria**:
- ✅ All invalid auth attempts are rejected
- ✅ Error code is UNAUTHORIZED
- ✅ HTTP status is 401

---

## Test Suite 8: Regression Tests

### Test 8.1: Existing Functionality Unchanged

**Objective**: Verify new features don't break existing functionality

**Tests**:
1. Deploy each token type without idempotency key
2. Verify all token types still work
3. Check subscription features still work
4. Verify health checks still work

**Pass Criteria**:
- ✅ All token deployments succeed
- ✅ No regressions in existing features
- ✅ API performance is acceptable

---

## Testing Checklist

### Pre-Testing Setup
- [ ] API endpoint configured
- [ ] Test accounts prepared
- [ ] Authentication working
- [ ] Test cards available (Stripe)
- [ ] Testing tool ready (Postman/curl)

### Error Handling
- [ ] Test 1.1: Validation error with hint
- [ ] Test 1.2: Blockchain connection error
- [ ] Test 1.3: Timeout error
- [ ] Verify all errors include correlation IDs

### Idempotency
- [ ] Test 2.1: Cache hit verification
- [ ] Test 2.2: All 11 endpoints tested
- [ ] Test 2.3: Expiration (if possible)
- [ ] Test 2.4: Optional key works

### Subscription
- [ ] Test 3.1: Create checkout session
- [ ] Test 3.2: Free tier status
- [ ] Test 3.3: Complete flow
- [ ] Test 3.4: Billing portal
- [ ] Test 3.5: Limit enforcement

### Deployment Status
- [ ] Test 4.1: Status transitions
- [ ] Test 4.2: Failed deployment
- [ ] Test 4.3: List & filter

### Health & Monitoring
- [ ] Test 5.1: Basic health
- [ ] Test 5.2: Detailed status
- [ ] Test 5.3: Degraded detection

### Audit Logging
- [ ] Test 6.1: Audit trail
- [ ] Test 6.2: Export CSV/JSON

### Authentication
- [ ] Test 7.1: Valid ARC-0014
- [ ] Test 7.2: Invalid auth

### Regression
- [ ] Test 8.1: No regressions

---

## Bug Reporting Template

When filing bugs, include:

```
**Test Case**: [Test number and name]
**Environment**: [Staging/Production/Local]
**Expected Behavior**: [What should happen]
**Actual Behavior**: [What actually happened]
**Steps to Reproduce**: 
1. 
2. 
3. 

**Error Details**:
- Error Code: 
- Error Message: 
- Correlation ID: 

**Request Details**:
- Endpoint: 
- Method: 
- Headers: 
- Body: 

**Additional Context**:
- Timestamp: 
- User Address: 
- Network: 
```

---

## Testing Environment

### Staging API
- **URL**: https://api-staging.biatectokens.com
- **Swagger**: https://api-staging.biatectokens.com/swagger

### Test Networks
- **Algorand Testnet**: https://testnet.algoexplorer.io
- **Base Sepolia**: https://sepolia.basescan.org

### Test Stripe Cards
- **Success**: 4242 4242 4242 4242
- **Decline**: 4000 0000 0000 0002
- **3D Secure**: 4000 0025 0000 3155

---

## Sign-off

**QA Engineer**: ___________________
**Date**: ___________________
**Test Result**: [ ] Pass [ ] Fail
**Notes**: 

---

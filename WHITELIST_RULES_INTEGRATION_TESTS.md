# Whitelisting Rules API - Integration Test Guide

## Overview

This document provides integration test scenarios for the RWA Whitelisting Rules API, specifically focusing on VOI and Aramid network compliance requirements.

## Prerequisites

- API running at `https://localhost:7000` or your deployment URL
- ARC-0014 authentication credentials for test user
- Test asset IDs on VOI and Aramid networks
- Test wallet addresses for whitelist entries

## Test Networks

### VOI Network
- Network ID: `voimain-v1.0`
- KYC Requirement: Recommended but not mandatory
- Test Asset ID: Use existing VOI asset

### Aramid Network
- Network ID: `aramidmain-v1.0`
- KYC Requirement: Mandatory for Active status (MICA compliance)
- Test Asset ID: Use existing Aramid asset

## Integration Test Scenarios

### Scenario 1: Create Rule for Auto-Revoke Expired Entries (VOI)

**Purpose**: Automatically revoke whitelist entries that have expired on VOI network.

**Test Steps**:
1. Create a rule:
```bash
curl -X POST https://localhost:7000/api/v1/whitelist-rules \
  -H "Authorization: SigTx <your-arc14-signed-tx>" \
  -H "Content-Type: application/json" \
  -d '{
    "assetId": 12345,
    "name": "Auto-Revoke Expired VOI Entries",
    "description": "Automatically revoke expired whitelist entries on VOI network",
    "ruleType": "AutoRevokeExpired",
    "isActive": true,
    "priority": 100,
    "network": "voimain-v1.0"
  }'
```

**Expected Response**:
- HTTP 200 OK
- Rule created with unique ID
- Audit log entry created

2. Verify rule was created:
```bash
curl -X GET https://localhost:7000/api/v1/whitelist-rules/12345?network=voimain-v1.0 \
  -H "Authorization: SigTx <your-arc14-signed-tx>"
```

**Expected Response**:
- HTTP 200 OK
- Rule appears in list
- Network field = "voimain-v1.0"

### Scenario 2: Require KYC for Active Status (Aramid)

**Purpose**: Enforce mandatory KYC verification for active whitelist entries on Aramid network per MICA requirements.

**Test Steps**:
1. Create whitelist entries (some with KYC, some without):
```bash
# Entry WITH KYC (should remain Active)
curl -X POST https://localhost:7000/api/v1/whitelist \
  -H "Authorization: SigTx <your-arc14-signed-tx>" \
  -H "Content-Type: application/json" \
  -d '{
    "assetId": 67890,
    "address": "ADDR1_ALGORAND_ADDRESS_58_CHARS_LONG_HERE",
    "status": "Active",
    "kycVerified": true,
    "kycProvider": "VerifyInc",
    "kycVerificationDate": "2026-01-15T00:00:00Z",
    "network": "aramidmain-v1.0",
    "role": "Admin"
  }'

# Entry WITHOUT KYC (should be deactivated by rule)
curl -X POST https://localhost:7000/api/v1/whitelist \
  -H "Authorization: SigTx <your-arc14-signed-tx>" \
  -H "Content-Type: application/json" \
  -d '{
    "assetId": 67890,
    "address": "ADDR2_ALGORAND_ADDRESS_58_CHARS_LONG_HERE",
    "status": "Active",
    "kycVerified": false,
    "network": "aramidmain-v1.0",
    "role": "Admin"
  }'
```

2. Create KYC enforcement rule:
```bash
curl -X POST https://localhost:7000/api/v1/whitelist-rules \
  -H "Authorization: SigTx <your-arc14-signed-tx>" \
  -H "Content-Type: application/json" \
  -d '{
    "assetId": 67890,
    "name": "Aramid KYC Requirement",
    "description": "MICA-compliant: KYC mandatory for Active status on Aramid",
    "ruleType": "RequireKycForActive",
    "isActive": true,
    "priority": 10,
    "network": "aramidmain-v1.0"
  }'
```

3. Apply the rule (dry run first):
```bash
curl -X POST https://localhost:7000/api/v1/whitelist-rules/apply \
  -H "Authorization: SigTx <your-arc14-signed-tx>" \
  -H "Content-Type: application/json" \
  -d '{
    "ruleId": "<rule-id-from-step-2>",
    "dryRun": true
  }'
```

**Expected Response**:
- HTTP 200 OK
- `affectedEntriesCount`: 1
- `affectedAddresses`: Contains ADDR2 only
- Actions describe deactivation of entry without KYC

4. Apply the rule (actual):
```bash
curl -X POST https://localhost:7000/api/v1/whitelist-rules/apply \
  -H "Authorization: SigTx <your-arc14-signed-tx>" \
  -H "Content-Type: application/json" \
  -d '{
    "ruleId": "<rule-id-from-step-2>",
    "dryRun": false
  }'
```

5. Verify whitelist entries updated:
```bash
curl -X GET https://localhost:7000/api/v1/whitelist/67890 \
  -H "Authorization: SigTx <your-arc14-signed-tx>"
```

**Expected Response**:
- ADDR1 status: Active (has KYC)
- ADDR2 status: Inactive (no KYC, deactivated by rule)

### Scenario 3: Network-Specific KYC Requirement

**Purpose**: Demonstrate network-specific rules where Aramid requires KYC but VOI does not.

**Test Steps**:
1. Create identical whitelist entries on both networks:
```bash
# VOI entry (no KYC, Active)
curl -X POST https://localhost:7000/api/v1/whitelist \
  -H "Authorization: SigTx <your-arc14-signed-tx>" \
  -H "Content-Type: application/json" \
  -d '{
    "assetId": 11111,
    "address": "TESTADDR_ALGORAND_ADDRESS_58_CHARS_LONG_HERE",
    "status": "Active",
    "kycVerified": false,
    "network": "voimain-v1.0"
  }'

# Aramid entry (no KYC, Active)
curl -X POST https://localhost:7000/api/v1/whitelist \
  -H "Authorization: SigTx <your-arc14-signed-tx>" \
  -H "Content-Type: application/json" \
  -d '{
    "assetId": 22222,
    "address": "TESTADDR_ALGORAND_ADDRESS_58_CHARS_LONG_HERE",
    "status": "Active",
    "kycVerified": false,
    "network": "aramidmain-v1.0"
  }'
```

2. Create network-specific KYC rule for Aramid only:
```bash
curl -X POST https://localhost:7000/api/v1/whitelist-rules \
  -H "Authorization: SigTx <your-arc14-signed-tx>" \
  -H "Content-Type: application/json" \
  -d '{
    "assetId": 22222,
    "name": "Aramid Network KYC Enforcement",
    "description": "Network-specific: KYC required only on Aramid",
    "ruleType": "NetworkKycRequirement",
    "isActive": true,
    "priority": 5,
    "network": "aramidmain-v1.0"
  }'
```

3. Apply the rule:
```bash
curl -X POST https://localhost:7000/api/v1/whitelist-rules/apply \
  -H "Authorization: SigTx <your-arc14-signed-tx>" \
  -H "Content-Type: application/json" \
  -d '{
    "ruleId": "<rule-id-from-step-2>",
    "dryRun": false
  }'
```

**Expected Results**:
- VOI entry (asset 11111): Remains Active (no KYC rule)
- Aramid entry (asset 22222): Changed to Inactive (KYC rule enforced)

### Scenario 4: Audit Log Verification (MICA Compliance)

**Purpose**: Verify complete audit trail for regulatory reporting.

**Test Steps**:
1. Perform several operations:
   - Create a rule
   - Update the rule (change priority)
   - Apply the rule
   - Deactivate the rule
   - Delete the rule

2. Retrieve audit log:
```bash
curl -X GET "https://localhost:7000/api/v1/whitelist-rules/12345/audit-log?fromDate=2026-01-24T00:00:00Z&toDate=2026-01-25T00:00:00Z" \
  -H "Authorization: SigTx <your-arc14-signed-tx>"
```

**Expected Response**:
- HTTP 200 OK
- Audit entries for all actions:
  - Create action with rule details
  - Update action with old and new state
  - Apply action with affected entries count
  - Deactivate action
  - Delete action
- Each entry includes:
  - Timestamp (performedAt)
  - User address (performedBy)
  - Network (if applicable)
  - Old/new state (for updates)

### Scenario 5: Rule Priority and Ordering

**Purpose**: Test that rules execute in priority order.

**Test Steps**:
1. Create multiple rules with different priorities:
```bash
# High priority (executes first)
curl -X POST https://localhost:7000/api/v1/whitelist-rules \
  -H "Authorization: SigTx <your-arc14-signed-tx>" \
  -H "Content-Type: application/json" \
  -d '{
    "assetId": 33333,
    "name": "High Priority Rule",
    "ruleType": "AutoRevokeExpired",
    "priority": 10,
    "isActive": true
  }'

# Low priority (executes last)
curl -X POST https://localhost:7000/api/v1/whitelist-rules \
  -H "Authorization: SigTx <your-arc14-signed-tx>" \
  -H "Content-Type: application/json" \
  -d '{
    "assetId": 33333,
    "name": "Low Priority Rule",
    "ruleType": "RequireKycForActive",
    "priority": 500,
    "isActive": true
  }'
```

2. List rules and verify order:
```bash
curl -X GET https://localhost:7000/api/v1/whitelist-rules/33333 \
  -H "Authorization: SigTx <your-arc14-signed-tx>"
```

**Expected Response**:
- Rules returned in priority order (10, then 500)
- Lower priority numbers appear first

### Scenario 6: Minimum KYC Age Rule

**Purpose**: Test rule with configuration parameter.

**Test Steps**:
1. Create rule with minimum KYC age requirement:
```bash
curl -X POST https://localhost:7000/api/v1/whitelist-rules \
  -H "Authorization: SigTx <your-arc14-signed-tx>" \
  -H "Content-Type: application/json" \
  -d '{
    "assetId": 44444,
    "name": "30-Day KYC Age Requirement",
    "description": "KYC must be at least 30 days old to be valid",
    "ruleType": "MinimumKycAge",
    "isActive": true,
    "priority": 50,
    "configuration": "{\"minimumDays\": 30}"
  }'
```

2. Create test entries with different KYC ages:
```bash
# Recent KYC (will be deactivated)
curl -X POST https://localhost:7000/api/v1/whitelist \
  -H "Authorization: SigTx <your-arc14-signed-tx>" \
  -H "Content-Type: application/json" \
  -d '{
    "assetId": 44444,
    "address": "RECENT_ALGORAND_ADDRESS_58_CHARS_LONG_HERE",
    "status": "Active",
    "kycVerified": true,
    "kycVerificationDate": "2026-01-20T00:00:00Z"
  }'

# Old KYC (will remain active)
curl -X POST https://localhost:7000/api/v1/whitelist \
  -H "Authorization: SigTx <your-arc14-signed-tx>" \
  -H "Content-Type: application/json" \
  -d '{
    "assetId": 44444,
    "address": "OLD_ALGORAND_ADDRESS_58_CHARS_LONG_HERE",
    "status": "Active",
    "kycVerified": true,
    "kycVerificationDate": "2025-12-01T00:00:00Z"
  }'
```

3. Apply rule and verify results

**Expected Results**:
- Recent KYC entry: Changed to Inactive
- Old KYC entry: Remains Active

## Error Handling Tests

### Test Invalid Rule Configuration
```bash
curl -X POST https://localhost:7000/api/v1/whitelist-rules \
  -H "Authorization: SigTx <your-arc14-signed-tx>" \
  -H "Content-Type: application/json" \
  -d '{
    "assetId": 55555,
    "name": "Invalid Config",
    "ruleType": "MinimumKycAge",
    "configuration": "{\"invalidKey\": \"value\"}"
  }'
```

**Expected Response**:
- HTTP 400 Bad Request
- Error message about missing required configuration

### Test Unauthorized Access
```bash
curl -X POST https://localhost:7000/api/v1/whitelist-rules \
  -H "Content-Type: application/json" \
  -d '{
    "assetId": 12345,
    "name": "Unauthorized Test",
    "ruleType": "AutoRevokeExpired"
  }'
```

**Expected Response**:
- HTTP 401 Unauthorized

### Test Apply Inactive Rule
```bash
# First create and deactivate a rule
curl -X PUT https://localhost:7000/api/v1/whitelist-rules \
  -H "Authorization: SigTx <your-arc14-signed-tx>" \
  -H "Content-Type: application/json" \
  -d '{
    "ruleId": "<rule-id>",
    "isActive": false
  }'

# Try to apply it
curl -X POST https://localhost:7000/api/v1/whitelist-rules/apply \
  -H "Authorization: SigTx <your-arc14-signed-tx>" \
  -H "Content-Type: application/json" \
  -d '{
    "ruleId": "<rule-id>",
    "dryRun": false
  }'
```

**Expected Response**:
- HTTP 400 Bad Request
- Error message: "Cannot apply inactive rule"

## Performance Tests

### Large-Scale Rule Application
1. Create 1000 whitelist entries
2. Create rule to affect all entries
3. Measure application time
4. Verify all entries updated correctly

**Expected Results**:
- Application completes in reasonable time (<5 seconds)
- All 1000 entries affected
- Audit log records single application event

## MICA Compliance Validation

### Required Features Checklist
- [ ] Audit log captures all rule lifecycle events
- [ ] User attribution in all operations
- [ ] Network-specific rules support (VOI/Aramid)
- [ ] KYC enforcement for Aramid network
- [ ] Timestamp accuracy for compliance reporting
- [ ] Complete state tracking (old/new values)
- [ ] Pagination for large audit logs
- [ ] Date range filtering for audit queries

## Test Data Cleanup

After integration tests, clean up test data:

```bash
# Delete test rules
curl -X DELETE https://localhost:7000/api/v1/whitelist-rules/<rule-id> \
  -H "Authorization: SigTx <your-arc14-signed-tx>"

# Remove test whitelist entries
curl -X DELETE https://localhost:7000/api/v1/whitelist \
  -H "Authorization: SigTx <your-arc14-signed-tx>" \
  -H "Content-Type: application/json" \
  -d '{
    "assetId": 12345,
    "address": "TEST_ADDRESS"
  }'
```

## Notes

- Replace `<your-arc14-signed-tx>` with actual ARC-0014 signed transaction
- Replace all placeholder addresses with valid 58-character Algorand addresses
- Use actual asset IDs from VOI and Aramid networks
- Run tests on testnet before production deployment
- Monitor audit logs after each operation for compliance verification

## Expected Test Results Summary

When all integration tests pass:
- ✅ Rules created successfully on both VOI and Aramid
- ✅ Network-specific KYC enforcement working
- ✅ Audit trail complete and queryable
- ✅ Rule priority ordering correct
- ✅ Configuration validation working
- ✅ Authentication enforced on all endpoints
- ✅ Error handling appropriate for all error conditions
- ✅ MICA compliance requirements met

# Compliance Capability Matrix API

## Overview

The Compliance Capability Matrix API provides a configurable, jurisdiction-aware capability system that defines which token standards, compliance checks, and transaction types are allowed per jurisdiction, wallet type, and KYC tier. This API serves as the single source of truth for compliance gating across the BiatecTokensApi platform.

## Business Value

- **Regulatory Compliance**: Ensures operations comply with jurisdiction-specific regulations
- **Transparency**: Provides clear, machine-readable policies for allowed operations
- **Risk Reduction**: Prevents unsupported operations before execution
- **Enterprise Ready**: Supports audit trails and compliance reporting
- **Scalability**: Configuration-driven approach scales with regulatory changes

## Architecture

### Components

1. **Capability Matrix Configuration** (`compliance-capabilities.json`)
   - JSON-based configuration file
   - Version-controlled and validated at startup
   - Defines rules per jurisdiction, wallet type, KYC tier, and token standard

2. **Capability Matrix Service** (`ICapabilityMatrixService`)
   - Loads and validates configuration
   - Evaluates capability rules
   - Provides caching for performance
   - Logs all capability decisions for audit

3. **Capability Matrix Controller** (`CapabilityMatrixController`)
   - REST API endpoints for querying capabilities
   - Filtering support for specific combinations
   - Structured error responses

## API Endpoints

### GET /api/v1/compliance/capabilities

Retrieves the compliance capability matrix with optional filtering.

**Query Parameters:**
- `jurisdiction` (optional): ISO country code (e.g., "US", "CH", "EU")
- `walletType` (optional): Wallet type (e.g., "custodial", "non-custodial")
- `tokenStandard` (optional): Token standard (e.g., "ARC-3", "ARC-19", "ARC-200", "ERC-20")
- `kycTier` (optional): KYC tier level (e.g., "1", "2", "3")

**Response (200 OK):**
```json
{
  "success": true,
  "data": {
    "version": "2026-02-05",
    "generatedAt": "2026-02-05T12:00:00Z",
    "jurisdictions": [
      {
        "code": "CH",
        "name": "Switzerland",
        "walletTypes": [
          {
            "type": "custodial",
            "description": "Custodial wallet with third-party control",
            "kycTiers": [
              {
                "tier": "2",
                "description": "Standard KYC with identity verification",
                "tokenStandards": [
                  {
                    "standard": "ARC-19",
                    "actions": ["mint", "transfer"],
                    "checks": ["sanctions", "accreditation"],
                    "notes": "Requires investor accreditation verification"
                  }
                ]
              }
            ]
          }
        ]
      }
    ]
  },
  "errorMessage": null,
  "errorDetails": null
}
```

**Response (404 Not Found):**
```json
{
  "success": false,
  "data": null,
  "errorMessage": "No matching capabilities found for the specified filters",
  "errorDetails": {
    "error": "no_matching_capabilities",
    "jurisdiction": "ZZ",
    "walletType": "custodial",
    "tokenStandard": "ARC-19",
    "kycTier": "2"
  }
}
```

**Examples:**

```bash
# Get all capabilities
curl -X GET "https://api.biatec.io/api/v1/compliance/capabilities"

# Get capabilities for Switzerland
curl -X GET "https://api.biatec.io/api/v1/compliance/capabilities?jurisdiction=CH"

# Get capabilities for custodial wallets in Switzerland with KYC tier 2
curl -X GET "https://api.biatec.io/api/v1/compliance/capabilities?jurisdiction=CH&walletType=custodial&kycTier=2"

# Get capabilities for ARC-19 tokens
curl -X GET "https://api.biatec.io/api/v1/compliance/capabilities?tokenStandard=ARC-19"
```

### POST /api/v1/compliance/capabilities/check

Checks if a specific action is allowed based on capability rules.

**Request Body:**
```json
{
  "jurisdiction": "CH",
  "walletType": "custodial",
  "tokenStandard": "ARC-19",
  "kycTier": "2",
  "action": "mint"
}
```

**Response (200 OK - Action Allowed):**
```json
{
  "allowed": true,
  "reason": null,
  "requiredChecks": ["sanctions", "accreditation"],
  "notes": "Requires investor accreditation verification",
  "errorDetails": null
}
```

**Response (403 Forbidden - Action Not Allowed):**
```json
{
  "allowed": false,
  "reason": "Action 'freeze' not allowed for token standard 'ARC-19' with KYC tier '2' in jurisdiction 'CH'",
  "requiredChecks": null,
  "notes": null,
  "errorDetails": {
    "error": "capability_not_allowed",
    "jurisdiction": "CH",
    "walletType": "custodial",
    "tokenStandard": "ARC-19",
    "kycTier": "2",
    "action": "freeze",
    "ruleId": "action_not_allowed"
  }
}
```

**Response (400 Bad Request - Validation Error):**
```json
{
  "allowed": false,
  "reason": "All fields (Jurisdiction, WalletType, TokenStandard, KycTier, Action) are required",
  "requiredChecks": null,
  "notes": null,
  "errorDetails": null
}
```

**Examples:**

```bash
# Check if minting is allowed
curl -X POST "https://api.biatec.io/api/v1/compliance/capabilities/check" \
  -H "Content-Type: application/json" \
  -d '{
    "jurisdiction": "CH",
    "walletType": "custodial",
    "tokenStandard": "ARC-19",
    "kycTier": "2",
    "action": "mint"
  }'

# Check if freeze is allowed (should be denied for tier 2)
curl -X POST "https://api.biatec.io/api/v1/compliance/capabilities/check" \
  -H "Content-Type: application/json" \
  -d '{
    "jurisdiction": "CH",
    "walletType": "custodial",
    "tokenStandard": "ARC-19",
    "kycTier": "2",
    "action": "freeze"
  }'
```

### GET /api/v1/compliance/capabilities/version

Gets the current capability matrix version.

**Response (200 OK):**
```json
{
  "version": "2026-02-05"
}
```

**Example:**

```bash
curl -X GET "https://api.biatec.io/api/v1/compliance/capabilities/version"
```

## Configuration

### Configuration File Structure

The capability matrix is defined in `compliance-capabilities.json`:

```json
{
  "version": "2026-02-05",
  "jurisdictions": [
    {
      "code": "CH",
      "name": "Switzerland",
      "walletTypes": [
        {
          "type": "custodial",
          "description": "Custodial wallet with third-party control",
          "kycTiers": [
            {
              "tier": "2",
              "description": "Standard KYC with identity verification",
              "tokenStandards": [
                {
                  "standard": "ARC-19",
                  "actions": ["mint", "transfer"],
                  "checks": ["sanctions", "accreditation"],
                  "notes": "Requires investor accreditation verification"
                }
              ]
            }
          ]
        }
      ]
    }
  ]
}
```

### Configuration Settings

In `appsettings.json`:

```json
{
  "CapabilityMatrixConfig": {
    "ConfigFilePath": "compliance-capabilities.json",
    "Version": "2026-02-05",
    "StrictMode": true,
    "EnableCaching": true,
    "CacheDurationSeconds": 3600
  }
}
```

**Configuration Options:**

- `ConfigFilePath`: Path to the capability matrix JSON file
- `Version`: Version identifier for the configuration
- `StrictMode`: When true, denies actions by default if no rule is found
- `EnableCaching`: Enables in-memory caching for performance
- `CacheDurationSeconds`: Cache duration in seconds (default: 3600 = 1 hour)

### Schema Definitions

#### Jurisdictions

Supported jurisdiction codes:
- `CH` - Switzerland
- `US` - United States
- `EU` - European Union
- `SG` - Singapore

#### Wallet Types

- `custodial` - Wallet controlled by a third-party service provider
- `non-custodial` - Self-custody wallet with user control
- `hardware` - Hardware wallet (cold storage)

#### KYC Tiers

- `0` - No KYC (anonymous)
- `1` - Basic KYC (email verification)
- `2` - Standard KYC (identity verification)
- `3` - Enhanced KYC (source of funds verification)

#### Token Standards

- `ARC-3` - Algorand fungible tokens with IPFS metadata
- `ARC-19` - Algorand security tokens
- `ARC-200` - Algorand smart contract tokens
- `ERC-20` - Ethereum fungible tokens (Base blockchain)

#### Actions

- `mint` - Create new tokens
- `transfer` - Transfer tokens between addresses
- `burn` - Destroy tokens
- `freeze` - Freeze/unfreeze tokens or accounts

#### Compliance Checks

- `sanctions` - OFAC/sanctions screening
- `accreditation` - Investor accreditation verification
- `sec_regulation_d` - SEC Regulation D compliance (US)
- `mica_compliance` - MiCA regulation compliance (EU)
- `gdpr_check` - GDPR data protection check (EU)
- `source_of_funds` - Source of funds verification
- `aml_check` - Anti-money laundering check
- `mas_accreditation` - MAS accreditation (Singapore)

## Audit Logging

All capability queries and enforcement decisions are logged with structured fields:

**Capability Query Log:**
```
Level: Information
Message: Capability matrix queried: Jurisdiction={Jurisdiction}, WalletType={WalletType}, TokenStandard={TokenStandard}, KycTier={KycTier}
```

**Capability Check Allow Log:**
```
Level: Information
Message: Capability check allowed: event=capability_check, decision=allow, context={Context}
```

**Capability Check Deny Log:**
```
Level: Warning
Message: Capability check denied: event=capability_check, decision=deny, ruleId={RuleId}, context={Context}
```

## Error Codes

| Error Code | Description | HTTP Status |
|------------|-------------|-------------|
| `no_matching_capabilities` | No capabilities found for the specified filters | 404 |
| `capability_not_allowed` | The requested action is not allowed | 403 |
| `jurisdiction_not_found` | Jurisdiction not in capability matrix | 403 |
| `wallet_type_not_supported` | Wallet type not supported for jurisdiction | 403 |
| `kyc_tier_not_supported` | KYC tier not supported for wallet type | 403 |
| `token_standard_not_supported` | Token standard not supported for KYC tier | 403 |
| `action_not_allowed` | Specific action not allowed for token standard | 403 |

## Integration Guide

### Client Integration

**1. Query Capabilities on Page Load**

```javascript
async function loadCapabilities() {
  const response = await fetch(
    '/api/v1/compliance/capabilities?jurisdiction=CH&walletType=custodial'
  );
  const result = await response.json();
  
  if (result.success) {
    // Update UI to show available token standards and actions
    updateUIWithCapabilities(result.data);
  }
}
```

**2. Check Before Action Execution**

```javascript
async function checkBeforeMint(params) {
  const response = await fetch(
    '/api/v1/compliance/capabilities/check',
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        jurisdiction: params.jurisdiction,
        walletType: params.walletType,
        tokenStandard: params.tokenStandard,
        kycTier: params.kycTier,
        action: 'mint'
      })
    }
  );
  
  const result = await response.json();
  
  if (result.allowed) {
    // Proceed with mint
    await executeMint(params);
  } else {
    // Show user-friendly error
    showError(result.reason);
  }
}
```

**3. Proactive UI Disabling**

```javascript
function updateUIBasedOnCapabilities(capabilities) {
  // Disable mint button if not allowed
  const mintAllowed = capabilities.data.jurisdictions[0]
    .walletTypes[0]
    .kycTiers[0]
    .tokenStandards[0]
    .actions.includes('mint');
  
  document.getElementById('mint-button').disabled = !mintAllowed;
}
```

### Backend Integration

**Enforce Capabilities in Token Workflows**

```csharp
public async Task<TokenCreationResponse> MintTokenAsync(MintTokenRequest request)
{
    // Check capability before minting
    var capabilityCheck = new CapabilityCheckRequest
    {
        Jurisdiction = request.Jurisdiction,
        WalletType = request.WalletType,
        TokenStandard = request.TokenStandard,
        KycTier = request.KycTier,
        Action = "mint"
    };
    
    var capabilityResult = await _capabilityMatrixService.CheckCapabilityAsync(capabilityCheck);
    
    if (!capabilityResult.Allowed)
    {
        return new TokenCreationResponse
        {
            Success = false,
            ErrorMessage = capabilityResult.Reason
        };
    }
    
    // Verify required checks
    foreach (var check in capabilityResult.RequiredChecks)
    {
        if (!await VerifyComplianceCheck(check, request))
        {
            return new TokenCreationResponse
            {
                Success = false,
                ErrorMessage = $"Compliance check '{check}' failed"
            };
        }
    }
    
    // Proceed with minting
    return await ExecuteMintAsync(request);
}
```

## Versioning Strategy

The capability matrix supports versioning to enable client caching and change detection:

1. **Version Field**: Each configuration includes a version string (e.g., "2026-02-05")
2. **Client Caching**: Clients can cache based on version and check periodically
3. **Backward Compatibility**: New capabilities are additive; existing capabilities remain valid
4. **Breaking Changes**: Version increment signals potential breaking changes

## Security Considerations

1. **Input Validation**: All user-provided inputs are sanitized before logging
2. **Deny by Default**: StrictMode ensures actions are denied unless explicitly allowed
3. **Audit Trail**: All capability decisions are logged for compliance reviews
4. **Configuration Validation**: Matrix configuration is validated at startup
5. **Read-Only API**: Capability matrix is read-only; changes require deployment

## Performance

- **Caching**: In-memory caching reduces configuration file reads
- **Cache Duration**: Default 1 hour, configurable via `CacheDurationSeconds`
- **Startup Validation**: Configuration loaded and validated at application startup
- **No Database**: File-based configuration for deterministic behavior

## Future Enhancements

1. **Dynamic Configuration**: Support hot-reloading without restart
2. **Database Storage**: Move configuration to database for multi-instance deployments
3. **Admin UI**: Web interface for managing capability rules
4. **Advanced Filters**: Support for investor type, token class, transaction value thresholds
5. **Rule Inheritance**: Hierarchical rules with inheritance
6. **Temporal Rules**: Time-based rule activation/deactivation

## Support

For questions or issues:
- GitHub Issues: https://github.com/scholtz/BiatecTokensApi/issues
- Documentation: See README.md in repository root
- API Documentation: Available at `/swagger` endpoint when running the API

# Compliance Validation Endpoint Documentation

## Overview

The compliance validation endpoint allows frontend applications to validate token configurations against MICA/RWA compliance rules before token deployment. This endpoint is designed to support token preset validation in the frontend, providing actionable feedback on missing controls and regulatory requirements.

## Endpoint

### Validate Token Configuration

**POST** `/api/v1/compliance/validate-preset`

Validates a token configuration against MICA/RWA compliance rules and returns detailed errors and warnings.

#### Authentication

Required: ARC-0014 authentication

#### Request Body

```json
{
  "assetType": "Security Token",
  "requiresAccreditedInvestors": true,
  "hasWhitelistControls": false,
  "hasIssuerControls": false,
  "verificationStatus": "Pending",
  "jurisdiction": null,
  "regulatoryFramework": null,
  "complianceStatus": null,
  "maxHolders": null,
  "network": "voimain-v1.0",
  "includeWarnings": true
}
```

**Field Descriptions:**

- `assetType` (string, optional): Type of asset being tokenized (e.g., "Security Token", "Utility Token", "NFT")
- `requiresAccreditedInvestors` (boolean): Whether the token requires accredited investors only
- `hasWhitelistControls` (boolean): Whether whitelist controls are enabled
- `hasIssuerControls` (boolean): Whether issuer controls (freeze, clawback) are enabled
- `verificationStatus` (string, optional): KYC verification status ("Pending", "InProgress", "Verified", "Failed", "Expired")
- `jurisdiction` (string, optional): Jurisdiction(s) where token is compliant (ISO country codes)
- `regulatoryFramework` (string, optional): Applicable regulatory framework(s)
- `complianceStatus` (string, optional): Compliance status ("UnderReview", "Compliant", "NonCompliant", "Suspended", "Exempt")
- `maxHolders` (integer, optional): Maximum number of token holders allowed
- `network` (string, optional): Blockchain network (e.g., "voimain-v1.0", "aramidmain-v1.0")
- `includeWarnings` (boolean, default: true): Whether to include warnings in the response

#### Response

**Success Response (200 OK):**

```json
{
  "success": true,
  "isValid": false,
  "errors": [
    {
      "severity": "Error",
      "field": "HasWhitelistControls",
      "message": "Tokens requiring accredited investors must have whitelist controls enabled",
      "recommendation": "Enable whitelist controls to restrict token transfers to verified accredited investors",
      "regulatoryContext": "Securities Act - Accredited Investor Requirements"
    },
    {
      "severity": "Error",
      "field": "Jurisdiction",
      "message": "Jurisdiction must be specified for security tokens and tokens requiring accredited investors",
      "recommendation": "Specify applicable jurisdiction(s) using ISO country codes (e.g., 'US', 'EU', 'US,EU')",
      "regulatoryContext": "MICA"
    }
  ],
  "warnings": [
    {
      "severity": "Warning",
      "field": "MaxHolders",
      "message": "Maximum number of holders is not specified for security token",
      "recommendation": "Consider setting a maximum holder limit to comply with securities regulations",
      "regulatoryContext": "Securities Regulations"
    }
  ],
  "summary": "Token configuration has 2 error(s) that must be fixed before deployment"
}
```

**Error Response (400 Bad Request):**

```json
{
  "success": false,
  "isValid": false,
  "errorMessage": "Invalid request parameters"
}
```

## Validation Rules

### MICA/RWA Compliance

#### Security Tokens
- **KYC Verification Required**: Security tokens must have `verificationStatus = "Verified"`
- **Jurisdiction Required**: Security tokens must specify applicable jurisdiction(s)
- **Accredited Investors**: Tokens requiring accredited investors must have whitelist controls enabled

#### Compliance Status
- **Regulatory Framework**: When `complianceStatus = "Compliant"`, regulatory framework must be specified

#### Holder Limits
- Security tokens should specify maximum number of holders

### Network-Specific Rules

#### VOI Network (`voimain-v1.0`)

1. **Accredited Investor Tokens**: Must have `verificationStatus = "Verified"`
2. **Jurisdiction Required**: All tokens must specify jurisdiction

#### Aramid Network (`aramidmain-v1.0`)

1. **Compliant Tokens**: Must specify regulatory framework when `complianceStatus = "Compliant"`
2. **Security Tokens**: Must specify `maxHolders`

### Token Controls

#### Whitelist Controls
- **Security Tokens**: Should have whitelist controls (warning if missing)
- **Accredited Investor Tokens**: Must have whitelist controls (error if missing)

#### Issuer Controls
- **Security Tokens**: Should have issuer controls (freeze, clawback) for regulatory compliance
- **RWA Tokens**: Benefit from issuer controls for compliance and dispute resolution

## Response Fields

### ValidationIssue

Each validation issue includes:

- `severity`: "Error" (must fix) or "Warning" (should review)
- `field`: Field name that has the issue
- `message`: Description of the issue
- `recommendation`: Actionable recommendation to resolve the issue
- `regulatoryContext`: Applicable regulation or standard (e.g., "MICA", "VOI Network Policy")

### Summary

The `summary` field provides a high-level overview:

- **Valid, No Warnings**: "Token configuration is valid and compliant with MICA/RWA requirements"
- **Valid, With Warnings**: "Token configuration is valid but has N warning(s) that should be reviewed"
- **Invalid**: "Token configuration has N error(s) that must be fixed before deployment"

## Examples

### Example 1: Valid Configuration

**Request:**
```json
{
  "assetType": "Security Token",
  "requiresAccreditedInvestors": true,
  "hasWhitelistControls": true,
  "hasIssuerControls": true,
  "verificationStatus": "Verified",
  "jurisdiction": "US",
  "regulatoryFramework": "SEC Reg D",
  "complianceStatus": "Compliant",
  "maxHolders": 500,
  "network": "voimain-v1.0",
  "includeWarnings": true
}
```

**Response:**
```json
{
  "success": true,
  "isValid": true,
  "errors": [],
  "warnings": [],
  "summary": "Token configuration is valid and compliant with MICA/RWA requirements"
}
```

### Example 2: Security Token Without Required Controls

**Request:**
```json
{
  "assetType": "Security Token",
  "requiresAccreditedInvestors": true,
  "hasWhitelistControls": false,
  "hasIssuerControls": false,
  "verificationStatus": "Pending",
  "network": "voimain-v1.0"
}
```

**Response:**
```json
{
  "success": true,
  "isValid": false,
  "errors": [
    {
      "severity": "Error",
      "field": "VerificationStatus",
      "message": "Security tokens require KYC verification to be completed (VerificationStatus=Verified)",
      "recommendation": "Complete KYC verification through your chosen provider before deploying the token",
      "regulatoryContext": "MICA (Markets in Crypto-Assets Regulation)"
    },
    {
      "severity": "Error",
      "field": "Jurisdiction",
      "message": "Jurisdiction must be specified for security tokens and tokens requiring accredited investors",
      "recommendation": "Specify applicable jurisdiction(s) using ISO country codes (e.g., 'US', 'EU', 'US,EU')",
      "regulatoryContext": "MICA"
    },
    {
      "severity": "Error",
      "field": "HasWhitelistControls",
      "message": "Tokens requiring accredited investors must have whitelist controls enabled",
      "recommendation": "Enable whitelist controls to restrict token transfers to verified accredited investors",
      "regulatoryContext": "Securities Act - Accredited Investor Requirements"
    }
  ],
  "warnings": [],
  "summary": "Token configuration has 3 error(s) that must be fixed before deployment"
}
```

### Example 3: Utility Token (Valid with Warnings)

**Request:**
```json
{
  "assetType": "Utility Token",
  "requiresAccreditedInvestors": false,
  "hasWhitelistControls": false,
  "hasIssuerControls": false,
  "includeWarnings": true
}
```

**Response:**
```json
{
  "success": true,
  "isValid": true,
  "errors": [],
  "warnings": [
    {
      "severity": "Warning",
      "field": "Jurisdiction",
      "message": "Jurisdiction is not specified. This may limit token distribution",
      "recommendation": "Consider specifying jurisdiction(s) to clarify regulatory compliance",
      "regulatoryContext": "MICA"
    }
  ],
  "summary": "Token configuration is valid but has 1 warning(s) that should be reviewed"
}
```

## Use Cases

### Frontend Token Presets

The validation endpoint is designed to support frontend token creation wizards and presets:

1. **Pre-Deployment Validation**: Validate configuration before submitting to blockchain
2. **Real-Time Feedback**: Show validation errors as users configure token parameters
3. **Preset Templates**: Validate preset templates to ensure they meet compliance requirements
4. **Educational Guidance**: Display recommendations to help users understand regulatory requirements

### Integration Example

```typescript
// Frontend integration example
async function validateTokenConfiguration(config) {
  const response = await fetch('/api/v1/compliance/validate-preset', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'Authorization': `SigTx ${signedTransaction}`
    },
    body: JSON.stringify(config)
  });
  
  const validation = await response.json();
  
  if (!validation.isValid) {
    // Display errors to user
    validation.errors.forEach(error => {
      console.error(`${error.field}: ${error.message}`);
      console.log(`Recommendation: ${error.recommendation}`);
    });
  }
  
  return validation;
}
```

## Best Practices

1. **Validate Early**: Call validation endpoint before submitting token deployment transaction
2. **Display All Issues**: Show both errors and warnings to users
3. **Include Context**: Display regulatory context to help users understand requirements
4. **Follow Recommendations**: Implementation recommendations are specific and actionable
5. **Network Selection**: Always specify target network for network-specific validation
6. **Progressive Enhancement**: Use warnings to guide users toward best practices without blocking deployment

## Status Codes

- **200 OK**: Validation completed successfully (check `isValid` field for result)
- **400 Bad Request**: Invalid request parameters
- **401 Unauthorized**: Missing or invalid authentication
- **500 Internal Server Error**: Server error

## Related Endpoints

- `POST /api/v1/compliance` - Create/update compliance metadata
- `GET /api/v1/compliance/{assetId}` - Get compliance metadata
- `POST /api/v1/whitelist/validate-transfer` - Validate token transfer
- `GET /api/v1/compliance/audit-log` - Compliance audit log

## Notes

- This endpoint does not modify any data; it only performs validation
- Validation is stateless and does not require pre-existing compliance metadata
- The endpoint is optimized for fast response times suitable for real-time UI feedback
- All recommendations include regulatory context for transparency

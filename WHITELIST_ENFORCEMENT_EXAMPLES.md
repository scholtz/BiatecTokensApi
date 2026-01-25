# Token Whitelisting API - Enforcement Examples for RWA Compliance

## Overview

This document demonstrates how to use the token whitelisting API to enforce MICA/RWA compliance on token operations. The whitelist system provides comprehensive management endpoints, transfer validation, and audit logging capabilities.

## Table of Contents

1. [Quick Start](#quick-start)
2. [Enforcement Mechanisms](#enforcement-mechanisms)
3. [Integration Examples](#integration-examples)
4. [Complete Flow Examples](#complete-flow-examples)
5. [Testing Compliance](#testing-compliance)

## Quick Start

### Prerequisites

- Token deployed on Algorand (ASA, ARC3, ARC200, ARC1400/ARC1644)
- ARC-0014 authentication configured
- Admin role for whitelist management

**Note**: ARC1400 (also known as ARC1644) is an Algorand security token standard that provides advanced compliance features including partitions, transfer restrictions, and regulatory controls.

### Basic Whitelist Setup

```bash
# 1. Add addresses to whitelist
POST /api/v1/whitelist
{
  "assetId": 12345,
  "address": "HOLDER_ADDRESS_HERE",
  "status": "Active",
  "kycVerified": true,
  "network": "voimain-v1.0"
}

# 2. Validate transfer before executing
POST /api/v1/whitelist/validate-transfer
{
  "assetId": 12345,
  "fromAddress": "SENDER_ADDRESS",
  "toAddress": "RECEIVER_ADDRESS"
}

# 3. Review audit log
GET /api/v1/whitelist/{assetId}/audit-log
```

## Enforcement Mechanisms

### 1. Pre-Transfer Validation (Recommended)

The most common approach is to validate transfers before submitting them to the blockchain.

```csharp
// In your token transfer service
public async Task<TransferResult> TransferTokenAsync(TransferRequest request)
{
    // Step 1: Validate whitelist compliance
    var validationRequest = new ValidateTransferRequest
    {
        AssetId = request.AssetId,
        FromAddress = request.FromAddress,
        ToAddress = request.ToAddress
    };
    
    var validation = await _whitelistService.ValidateTransferAsync(
        validationRequest, 
        currentUser
    );
    
    // Step 2: Block if not allowed
    if (!validation.IsAllowed)
    {
        _logger.LogWarning(
            "Transfer blocked by whitelist: {Reason}", 
            validation.DenialReason
        );
        
        return new TransferResult
        {
            Success = false,
            ErrorMessage = $"Transfer not allowed: {validation.DenialReason}"
        };
    }
    
    // Step 3: Proceed with blockchain transfer
    var txResult = await ExecuteBlockchainTransfer(request);
    
    return txResult;
}
```

### 2. Controller-Level Enforcement

Apply the `WhitelistEnforcementAttribute` to automatically enforce whitelist checks on controller actions.

```csharp
[HttpPost("transfer")]
[WhitelistEnforcement(
    AssetIdParameter = "assetId",
    AddressParameters = new[] { "fromAddress", "toAddress" }
)]
public async Task<IActionResult> TransferToken([FromBody] TransferRequest request)
{
    // Whitelist is automatically validated before this method executes
    // If validation fails, returns 403 Forbidden with denial reason
    
    var result = await _tokenService.TransferAsync(request);
    return Ok(result);
}
```

### 3. Mint Operation Enforcement

For tokens with minting capabilities, enforce whitelist on the recipient address.

```csharp
[HttpPost("mint")]
[WhitelistEnforcement(
    AssetIdParameter = "assetId",
    AddressParameters = new[] { "recipientAddress" }
)]
public async Task<IActionResult> MintTokens([FromBody] MintRequest request)
{
    // Whitelist validation ensures only whitelisted addresses can receive minted tokens
    
    var result = await _tokenService.MintAsync(request);
    return Ok(result);
}
```

## Integration Examples

### Example 1: ARC200 Token with Whitelist Enforcement

This example shows how to integrate whitelist enforcement into an ARC200 token transfer endpoint.

```csharp
using BiatecTokensApi.Filters;
using BiatecTokensApi.Models.ARC200.Request;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Authorize]
[ApiController]
[Route("api/v1/arc200")]
public class ARC200Controller : ControllerBase
{
    private readonly IARC200TokenService _tokenService;
    private readonly IWhitelistService _whitelistService;
    private readonly ILogger<ARC200Controller> _logger;

    public ARC200Controller(
        IARC200TokenService tokenService,
        IWhitelistService whitelistService,
        ILogger<ARC200Controller> logger)
    {
        _tokenService = tokenService;
        _whitelistService = whitelistService;
        _logger = logger;
    }

    /// <summary>
    /// Transfer ARC200 tokens with whitelist enforcement
    /// </summary>
    [HttpPost("{assetId}/transfer")]
    [WhitelistEnforcement(
        AssetIdParameter = "assetId",
        AddressParameters = new[] { "fromAddress", "toAddress" }
    )]
    [ProducesResponseType(typeof(TransferResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Transfer(
        [FromRoute] ulong assetId,
        [FromBody] ARC200TransferRequest request)
    {
        // At this point, whitelist has been automatically validated
        // Both fromAddress and toAddress are confirmed as whitelisted and active
        
        try
        {
            request.AssetId = assetId;
            var result = await _tokenService.TransferAsync(request);
            
            if (result.Success)
            {
                _logger.LogInformation(
                    "ARC200 transfer completed: Asset={AssetId}, From={From}, To={To}, Amount={Amount}",
                    assetId, request.FromAddress, request.ToAddress, request.Amount
                );
                return Ok(result);
            }
            else
            {
                _logger.LogError(
                    "ARC200 transfer failed: {Error}",
                    result.ErrorMessage
                );
                return StatusCode(StatusCodes.Status500InternalServerError, result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during ARC200 transfer");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                errorMessage = "Transfer failed due to internal error"
            });
        }
    }

    /// <summary>
    /// Mint ARC200 tokens to a whitelisted address
    /// </summary>
    [HttpPost("{assetId}/mint")]
    [WhitelistEnforcement(
        AssetIdParameter = "assetId",
        AddressParameters = new[] { "toAddress" }
    )]
    [ProducesResponseType(typeof(MintResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Mint(
        [FromRoute] ulong assetId,
        [FromBody] ARC200MintRequest request)
    {
        // Whitelist enforcement ensures toAddress is whitelisted
        
        request.AssetId = assetId;
        var result = await _tokenService.MintAsync(request);
        
        return result.Success ? Ok(result) : StatusCode(500, result);
    }
}
```

### Example 2: Manual Validation in Service Layer

For more complex scenarios, perform manual validation in the service layer.

```csharp
public class RWATokenService : IRWATokenService
{
    private readonly IWhitelistService _whitelistService;
    private readonly ILogger<RWATokenService> _logger;

    public async Task<TransferResult> TransferWithComplianceChecksAsync(
        TransferRequest request,
        string performedBy)
    {
        // Step 1: Validate transfer is whitelisted
        var validation = await _whitelistService.ValidateTransferAsync(
            new ValidateTransferRequest
            {
                AssetId = request.AssetId,
                FromAddress = request.FromAddress,
                ToAddress = request.ToAddress
            },
            performedBy
        );

        if (!validation.Success)
        {
            return new TransferResult
            {
                Success = false,
                ErrorMessage = validation.ErrorMessage ?? "Whitelist validation failed"
            };
        }

        if (!validation.IsAllowed)
        {
            // Log denial for compliance tracking
            _logger.LogWarning(
                "RWA transfer denied: Asset={AssetId}, From={From}, To={To}, Reason={Reason}",
                request.AssetId,
                request.FromAddress,
                request.ToAddress,
                validation.DenialReason
            );

            return new TransferResult
            {
                Success = false,
                ErrorMessage = validation.DenialReason,
                IsComplianceBlocked = true
            };
        }

        // Step 2: Additional business logic checks
        if (!await CheckTransferLimits(request))
        {
            return new TransferResult
            {
                Success = false,
                ErrorMessage = "Transfer exceeds daily limit"
            };
        }

        // Step 3: Execute blockchain transaction
        try
        {
            var txResult = await ExecuteBlockchainTransfer(request);

            // Step 4: Record successful transfer in audit log
            // (This is automatically done by ValidateTransferAsync, but you can add custom logging)
            _logger.LogInformation(
                "RWA transfer completed: TxId={TxId}, Asset={AssetId}",
                txResult.TransactionId,
                request.AssetId
            );

            return txResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Blockchain transfer failed");
            return new TransferResult
            {
                Success = false,
                ErrorMessage = "Blockchain transaction failed"
            };
        }
    }
}
```

## Complete Flow Examples

### Flow 1: Adding Addresses to Whitelist

```bash
# 1. Admin authenticates with ARC-0014
# Note: Generate the ARC-14 signed transaction using your Algorand wallet SDK
# Example: const signedTx = await wallet.signTransaction(authTransaction);
# Then base64 encode it: btoa(String.fromCharCode(...signedTx))

# 2. Add addresses to whitelist

# Add investor 1
curl -X POST https://api.biatectokens.com/api/v1/whitelist \
  -H "Authorization: SigTx <base64-encoded-arc14-signed-transaction>" \
  -H "Content-Type: application/json" \
  -d '{
    "assetId": 12345,
    "address": "INVESTOR1_ALGORAND_ADDRESS_HERE",
    "status": "Active",
    "reason": "Accredited investor - KYC completed",
    "kycVerified": true,
    "kycVerificationDate": "2026-01-25T00:00:00Z",
    "kycProvider": "VerifyInvest Inc",
    "network": "voimain-v1.0",
    "role": "Operator"
  }'

# Add investor 2
curl -X POST https://api.biatectokens.com/api/v1/whitelist \
  -H "Authorization: SigTx <arc14-signed-tx>" \
  -H "Content-Type: application/json" \
  -d '{
    "assetId": 12345,
    "address": "INVESTOR2_ALGORAND_ADDRESS_HERE",
    "status": "Active",
    "kycVerified": true,
    "network": "voimain-v1.0",
    "role": "Operator"
  }'

# 3. Verify entries were added
curl -X GET https://api.biatectokens.com/api/v1/whitelist/12345?status=Active \
  -H "Authorization: SigTx <arc14-signed-tx>"
```

### Flow 2: Token Transfer with Validation

```bash
# 1. Before initiating transfer, validate addresses
curl -X POST https://api.biatectokens.com/api/v1/whitelist/validate-transfer \
  -H "Authorization: SigTx <arc14-signed-tx>" \
  -H "Content-Type: application/json" \
  -d '{
    "assetId": 12345,
    "fromAddress": "INVESTOR1_ALGORAND_ADDRESS_HERE",
    "toAddress": "INVESTOR2_ALGORAND_ADDRESS_HERE"
  }'

# Response when allowed:
{
  "success": true,
  "isAllowed": true,
  "senderStatus": {
    "address": "INVESTOR1_ALGORAND_ADDRESS_HERE",
    "isWhitelisted": true,
    "isActive": true,
    "isExpired": false,
    "status": "Active"
  },
  "receiverStatus": {
    "address": "INVESTOR2_ALGORAND_ADDRESS_HERE",
    "isWhitelisted": true,
    "isActive": true,
    "isExpired": false,
    "status": "Active"
  }
}

# 2. If validation passes, execute the transfer
# (Your transfer endpoint implementation)

# 3. Check audit log to verify the validation was recorded
curl -X GET "https://api.biatectokens.com/api/v1/whitelist/12345/audit-log?actionType=TransferValidation" \
  -H "Authorization: SigTx <arc14-signed-tx>"
```

### Flow 3: Denied Transfer Scenario

```bash
# 1. Attempt to transfer to non-whitelisted address
curl -X POST https://api.biatectokens.com/api/v1/whitelist/validate-transfer \
  -H "Authorization: SigTx <arc14-signed-tx>" \
  -H "Content-Type: application/json" \
  -d '{
    "assetId": 12345,
    "fromAddress": "INVESTOR1_ALGORAND_ADDRESS_HERE",
    "toAddress": "NON_WHITELISTED_ADDRESS_HERE"
  }'

# Response when denied:
{
  "success": true,
  "isAllowed": false,
  "denialReason": "Receiver address NON_WHITELISTED_ADDRESS_HERE is not whitelisted for asset 12345",
  "senderStatus": {
    "address": "INVESTOR1_ALGORAND_ADDRESS_HERE",
    "isWhitelisted": true,
    "isActive": true,
    "isExpired": false,
    "status": "Active"
  },
  "receiverStatus": {
    "address": "NON_WHITELISTED_ADDRESS_HERE",
    "isWhitelisted": false,
    "isActive": false,
    "isExpired": false,
    "status": null
  }
}

# 2. The transfer should NOT be executed
# The application must respect the denial and not submit the transaction to blockchain

# 3. Denial is automatically logged in audit trail
curl -X GET "https://api.biatectokens.com/api/v1/whitelist/12345/audit-log?actionType=TransferValidation&fromDate=2026-01-25T00:00:00Z" \
  -H "Authorization: SigTx <arc14-signed-tx>"
```

### Flow 4: Bulk Address Upload

```bash
# For initial token deployment with many known investors
curl -X POST https://api.biatectokens.com/api/v1/whitelist/bulk \
  -H "Authorization: SigTx <arc14-signed-tx>" \
  -H "Content-Type: application/json" \
  -d '{
    "assetId": 12345,
    "entries": [
      {
        "address": "INVESTOR1_ADDRESS",
        "status": "Active",
        "kycVerified": true,
        "network": "voimain-v1.0"
      },
      {
        "address": "INVESTOR2_ADDRESS",
        "status": "Active",
        "kycVerified": true,
        "network": "voimain-v1.0"
      },
      {
        "address": "INVESTOR3_ADDRESS",
        "status": "Active",
        "kycVerified": true,
        "network": "voimain-v1.0"
      }
    ],
    "network": "voimain-v1.0"
  }'

# Response includes results for each address
{
  "success": true,
  "addedCount": 3,
  "failedCount": 0,
  "results": [
    {
      "address": "INVESTOR1_ADDRESS",
      "success": true
    },
    {
      "address": "INVESTOR2_ADDRESS",
      "success": true
    },
    {
      "address": "INVESTOR3_ADDRESS",
      "success": true
    }
  ]
}
```

### Flow 5: Compliance Audit Export

```bash
# Export audit log for regulatory reporting
curl -X GET "https://api.biatectokens.com/api/v1/whitelist/audit-log/export/csv?assetId=12345&fromDate=2026-01-01T00:00:00Z&toDate=2026-12-31T23:59:59Z" \
  -H "Authorization: SigTx <arc14-signed-tx>" \
  -o compliance-audit-2026.csv

# Or export as JSON
curl -X GET "https://api.biatectokens.com/api/v1/whitelist/audit-log/export/json?assetId=12345&fromDate=2026-01-01T00:00:00Z&toDate=2026-12-31T23:59:59Z" \
  -H "Authorization: SigTx <arc14-signed-tx>" \
  -o compliance-audit-2026.json
```

## Testing Compliance

### Unit Test Example

```csharp
[Test]
public async Task Transfer_ToNonWhitelistedAddress_ShouldBeBlocked()
{
    // Arrange
    var assetId = (ulong)12345;
    var whitelistedAddress = "WHITELISTED_ADDRESS";
    var nonWhitelistedAddress = "NON_WHITELISTED_ADDRESS";

    // Add only sender to whitelist
    await _whitelistService.AddEntryAsync(new AddWhitelistEntryRequest
    {
        AssetId = assetId,
        Address = whitelistedAddress,
        Status = WhitelistStatus.Active,
        Network = "voimain-v1.0"
    }, "ADMIN_ADDRESS");

    // Act - Try to validate transfer to non-whitelisted address
    var validation = await _whitelistService.ValidateTransferAsync(
        new ValidateTransferRequest
        {
            AssetId = assetId,
            FromAddress = whitelistedAddress,
            ToAddress = nonWhitelistedAddress
        },
        "ADMIN_ADDRESS"
    );

    // Assert
    Assert.That(validation.Success, Is.True);
    Assert.That(validation.IsAllowed, Is.False);
    Assert.That(validation.DenialReason, Does.Contain("not whitelisted"));
    Assert.That(validation.SenderStatus.IsWhitelisted, Is.True);
    Assert.That(validation.ReceiverStatus.IsWhitelisted, Is.False);
}

[Test]
public async Task Transfer_BothAddressesWhitelisted_ShouldBeAllowed()
{
    // Arrange
    var assetId = (ulong)12345;
    var senderAddress = "SENDER_ADDRESS";
    var receiverAddress = "RECEIVER_ADDRESS";

    // Add both addresses to whitelist
    await _whitelistService.AddEntryAsync(new AddWhitelistEntryRequest
    {
        AssetId = assetId,
        Address = senderAddress,
        Status = WhitelistStatus.Active,
        Network = "voimain-v1.0"
    }, "ADMIN_ADDRESS");

    await _whitelistService.AddEntryAsync(new AddWhitelistEntryRequest
    {
        AssetId = assetId,
        Address = receiverAddress,
        Status = WhitelistStatus.Active,
        Network = "voimain-v1.0"
    }, "ADMIN_ADDRESS");

    // Act
    var validation = await _whitelistService.ValidateTransferAsync(
        new ValidateTransferRequest
        {
            AssetId = assetId,
            FromAddress = senderAddress,
            ToAddress = receiverAddress
        },
        "ADMIN_ADDRESS"
    );

    // Assert
    Assert.That(validation.Success, Is.True);
    Assert.That(validation.IsAllowed, Is.True);
    Assert.That(validation.DenialReason, Is.Null);
    Assert.That(validation.SenderStatus.IsWhitelisted, Is.True);
    Assert.That(validation.SenderStatus.IsActive, Is.True);
    Assert.That(validation.ReceiverStatus.IsWhitelisted, Is.True);
    Assert.That(validation.ReceiverStatus.IsActive, Is.True);
}
```

### Integration Test Example

```csharp
[TestFixture]
public class WhitelistEnforcementIntegrationTests
{
    private WebApplicationFactory<Program> _factory;
    private HttpClient _client;
    private string _authToken;

    [SetUp]
    public void Setup()
    {
        _factory = new WebApplicationFactory<Program>();
        _client = _factory.CreateClient();
        _authToken = GenerateARC14AuthToken();
    }

    [Test]
    public async Task EndToEnd_WhitelistEnforcement_ShouldPreventUnauthorizedTransfer()
    {
        var assetId = 12345;
        
        // 1. Add sender to whitelist
        var addRequest = new
        {
            assetId = assetId,
            address = "SENDER_ADDRESS",
            status = "Active",
            network = "voimain-v1.0"
        };

        var addResponse = await _client.PostAsJsonAsync(
            "/api/v1/whitelist",
            addRequest,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
        );
        
        Assert.That(addResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // 2. Attempt transfer to non-whitelisted address (should fail)
        var transferRequest = new
        {
            assetId = assetId,
            fromAddress = "SENDER_ADDRESS",
            toAddress = "NON_WHITELISTED_ADDRESS",
            amount = 1000
        };

        var transferResponse = await _client.PostAsJsonAsync(
            $"/api/v1/arc200/{assetId}/transfer",
            transferRequest
        );

        // 3. Verify transfer was blocked
        Assert.That(transferResponse.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));

        var errorContent = await transferResponse.Content.ReadAsStringAsync();
        Assert.That(errorContent, Does.Contain("not whitelisted"));

        // 4. Verify denial was logged in audit trail
        var auditResponse = await _client.GetAsync(
            $"/api/v1/whitelist/{assetId}/audit-log?actionType=TransferValidation"
        );
        
        Assert.That(auditResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        
        var auditContent = await auditResponse.Content.ReadFromJsonAsync<AuditLogResponse>();
        Assert.That(auditContent.Entries, Is.Not.Empty);
        
        var lastEntry = auditContent.Entries.First();
        Assert.That(lastEntry.ActionType, Is.EqualTo("TransferValidation"));
        Assert.That(lastEntry.Address, Is.EqualTo("NON_WHITELISTED_ADDRESS"));
    }
}
```

## Best Practices

### 1. Always Validate Before Transfer

Never execute a blockchain transaction without first validating the whitelist status:

```csharp
// ✅ CORRECT - Always validate before executing transfer
var validation = await ValidateTransfer(request);
if (validation.IsAllowed)
{
    await ExecuteBlockchainTransfer(request);
}

// ❌ INCORRECT - No validation
// RISK: Violates compliance requirements, exposes to regulatory penalties,
//       could result in tokens being transferred to sanctioned addresses,
//       no audit trail of compliance checks
await ExecuteBlockchainTransfer(request);
```

### 2. Use Attribute-Based Enforcement

For consistent enforcement across all endpoints, use the `WhitelistEnforcementAttribute`:

```csharp
// ✅ CORRECT - Declarative enforcement
[WhitelistEnforcement(AssetIdParameter = "assetId", AddressParameters = new[] { "fromAddress", "toAddress" })]
public async Task<IActionResult> Transfer([FromBody] TransferRequest request)

// ❌ LESS IDEAL - Manual validation in every endpoint
public async Task<IActionResult> Transfer([FromBody] TransferRequest request)
{
    var validation = await _whitelistService.ValidateTransferAsync(...);
    // Risk of forgetting to validate in some endpoints
}
```

### 3. Log All Enforcement Events

Comprehensive audit logging is crucial for compliance:

```csharp
_logger.LogWarning(
    "Transfer blocked by whitelist: Asset={AssetId}, From={From}, To={To}, Reason={Reason}",
    request.AssetId,
    request.FromAddress,
    request.ToAddress,
    validation.DenialReason
);
```

### 4. Provide Clear Error Messages

Users should understand why their operation was blocked:

```csharp
return new ObjectResult(new
{
    success = false,
    errorMessage = $"Transfer not allowed: {validation.DenialReason}",
    complianceInfo = "Please contact support to add this address to the whitelist."
})
{
    StatusCode = StatusCodes.Status403Forbidden
};
```

### 5. Regular Audit Log Reviews

Set up periodic reviews of the audit log for compliance monitoring:

```bash
# Weekly compliance report
curl -X GET "https://api.biatectokens.com/api/v1/whitelist/audit-log/export/csv?fromDate=$(date -d '7 days ago' -I)&toDate=$(date -I)" \
  -H "Authorization: SigTx <arc14-signed-tx>" \
  -o weekly-compliance-report.csv
```

## Compliance Benefits

### MICA Alignment

- ✅ **7-year audit retention** - All whitelist changes tracked with immutable logs
- ✅ **Transfer validation** - Pre-transaction compliance checks
- ✅ **Network-specific rules** - VOI and Aramid compliance enforcement
- ✅ **KYC tracking** - Integration with KYC providers
- ✅ **Exportable audit trails** - CSV/JSON exports for regulators

### Enterprise Readiness

- ✅ **Role-based access** - Admin vs Operator roles
- ✅ **Bulk operations** - Efficient management of large whitelists
- ✅ **Status lifecycle** - Active, Inactive, Revoked states
- ✅ **Expiration dates** - Time-limited whitelist entries
- ✅ **Comprehensive API** - Full CRUD operations with filtering

## Additional Resources

- [Frontend Integration Guide](./RWA_WHITELIST_FRONTEND_INTEGRATION.md) - Complete guide for frontend developers
- [Whitelist Feature Overview](./WHITELIST_FEATURE.md) - Business value and technical overview
- [Enforcement Implementation](./WHITELIST_ENFORCEMENT_IMPLEMENTATION.md) - Technical implementation details
- [Audit Log API](./WHITELIST_AUDIT_ENDPOINTS_IMPLEMENTATION.md) - Audit log endpoint documentation
- [Business Value Document](./RWA_WHITELIST_BUSINESS_VALUE.md) - ROI and business case

## Support

For questions or issues:
- GitHub Issues: https://github.com/scholtz/BiatecTokensApi/issues
- API Documentation: https://api.biatectokens.com/swagger
- Email: support@biatectokens.com

---

**Last Updated**: January 25, 2026  
**API Version**: v1.0  
**Compliance Standards**: MICA, KYC/AML

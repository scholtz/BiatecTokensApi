# Manual Verification Checklist for KMS/HSM Key Management

## Prerequisites
- Azure account with Key Vault access OR AWS account with Secrets Manager access
- .NET 10.0 SDK installed
- Access to clone the repository

## Test Scenarios

### Scenario 1: Development with Hardcoded Provider

**Environment:** Local Development

**Steps:**
1. Clone repository: `git clone https://github.com/scholtz/BiatecTokensApi.git`
2. Checkout branch: `git checkout copilot/implement-kms-hsm-key-management`
3. Configure `appsettings.Development.json`:
```json
{
  "KeyManagementConfig": {
    "Provider": "Hardcoded",
    "HardcodedKey": "DevTestKey32CharactersMinimumForAES256Encryption"
  }
}
```
4. Run: `dotnet run --project BiatecTokensApi/BiatecTokensApi.csproj`
5. Navigate to: `https://localhost:7000/health`

**Expected Outcome:**
- ✅ Application starts successfully
- ✅ Health endpoint returns 200 OK
- ✅ Logs show: "Hardcoded key provider is configured - NOT RECOMMENDED FOR PRODUCTION"
- ✅ Swagger UI accessible at https://localhost:7000/swagger

### Scenario 2: Azure Key Vault (Managed Identity)

**Environment:** Azure App Service or Azure VM

**Steps:**
1. Create Azure Key Vault:
```bash
az keyvault create --name biatec-kms-test --resource-group mygroup --location eastus
```

2. Store encryption key:
```bash
az keyvault secret set --vault-name biatec-kms-test \
  --name encryption-key \
  --value "$(openssl rand -base64 48)"
```

3. Enable managed identity on your Azure resource
4. Grant access to Key Vault:
```bash
az keyvault set-policy --name biatec-kms-test \
  --object-id <managed-identity-principal-id> \
  --secret-permissions get
```

5. Configure `appsettings.Production.json`:
```json
{
  "KeyManagementConfig": {
    "Provider": "AzureKeyVault",
    "AzureKeyVault": {
      "VaultUrl": "https://biatec-kms-test.vault.azure.net/",
      "SecretName": "encryption-key",
      "UseManagedIdentity": true
    }
  }
}
```

6. Deploy and start application
7. Check `/health` endpoint

**Expected Outcome:**
- ✅ Application starts successfully
- ✅ Health check reports "Key provider 'AzureKeyVault' is healthy"
- ✅ Logs show: "Successfully retrieved encryption key from Azure Key Vault"
- ✅ Token creation endpoints work correctly

### Scenario 3: AWS Secrets Manager (IAM Role)

**Environment:** AWS EC2, ECS, or EKS

**Steps:**
1. Create secret in AWS Secrets Manager:
```bash
aws secretsmanager create-secret \
  --name biatec/encryption-key \
  --secret-string "$(openssl rand -base64 48)" \
  --region us-east-1
```

2. Create IAM policy:
```json
{
  "Version": "2012-10-17",
  "Statement": [{
    "Effect": "Allow",
    "Action": ["secretsmanager:GetSecretValue"],
    "Resource": "arn:aws:secretsmanager:us-east-1:*:secret:biatec/encryption-key-*"
  }]
}
```

3. Attach policy to EC2/ECS role
4. Configure `appsettings.Production.json`:
```json
{
  "KeyManagementConfig": {
    "Provider": "AwsKms",
    "AwsKms": {
      "Region": "us-east-1",
      "KeyId": "biatec/encryption-key",
      "UseIamRole": true
    }
  }
}
```

5. Deploy and start application
6. Check `/health` endpoint

**Expected Outcome:**
- ✅ Application starts successfully
- ✅ Health check reports "Key provider 'AwsKms' is healthy"
- ✅ Logs show: "Successfully retrieved encryption key from AWS Secrets Manager"
- ✅ Token creation endpoints work correctly

### Scenario 4: Production Environment Detection (Negative Test)

**Environment:** Any with ASPNETCORE_ENVIRONMENT=Production

**Steps:**
1. Set environment: `export ASPNETCORE_ENVIRONMENT=Production`
2. Configure insecure provider:
```json
{
  "KeyManagementConfig": {
    "Provider": "EnvironmentVariable",
    "EnvironmentVariableName": "BIATEC_ENCRYPTION_KEY"
  }
}
```
3. Try to start application

**Expected Outcome:**
- ❌ Application FAILS to start
- ❌ Health check returns Unhealthy
- ❌ Error message: "Insecure key management provider detected in production"
- ❌ Error code: KMS_INSECURE_PROVIDER_IN_PRODUCTION

## Test Results Verification

### Unit Tests
```bash
dotnet test --filter "FullyQualifiedName~KeyProvider" --configuration Release
```
**Expected:** 23/23 passing

### Integration Tests
```bash
dotnet test --filter "FullyQualifiedName~KeyManagementIntegrationTests" --configuration Release
```
**Expected:** 10/10 passing

### All Tests
```bash
dotnet test --filter "FullyQualifiedName!~RealEndpoint" --configuration Release
```
**Expected:** 1481+ tests, 0 failures

## Security Verification

### Verify No Secrets in Logs
1. Check application logs
2. Confirm no encryption keys are logged
3. Verify only sanitized inputs in logs
4. Check correlation IDs are present

### Verify Fail-Closed Behavior
1. Misconfigure KMS (wrong URL/ID)
2. Confirm application fails to start
3. Verify clear error message
4. Check no fallback to insecure provider

## Performance Verification

### Client Pooling
1. Monitor memory usage during key retrieval
2. Verify only one client instance created per provider
3. Check connection reuse with multiple operations
4. Confirm no memory leaks after multiple requests

## Documentation Review

### KEY_MANAGEMENT_GUIDE.md
- ✅ Azure Key Vault setup documented
- ✅ AWS Secrets Manager setup documented
- ✅ Key rotation procedures documented
- ✅ Rollback procedures documented
- ✅ Cost estimates provided

## Sign-Off Checklist

- [ ] All test scenarios executed successfully
- [ ] Production environment detection verified
- [ ] No secrets visible in logs
- [ ] Performance acceptable (< 500ms key retrieval)
- [ ] Documentation reviewed and accurate
- [ ] Ready for production deployment

---
**Reviewer Name:** _________________
**Date:** _________________
**Environment:** Dev / Staging / Production
**Notes:** _________________

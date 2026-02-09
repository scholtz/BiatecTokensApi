# Key Management System - Production Deployment Guide

## Overview

The BiatecTokensApi uses a flexible key management system to secure user mnemonics (private keys) at rest. This guide explains how to configure and deploy the key management system for production environments.

## Security Architecture

### Encryption Flow

```
User Registration → Generate BIP39 Mnemonic (24 words, 256-bit entropy)
                  ↓
        ARC76 Account Derivation (deterministic Algorand address)
                  ↓
        Encrypt Mnemonic with System Key (AES-256-GCM)
                  ↓
        Store Encrypted Mnemonic in Database
                  ↓
        Decrypt on-demand for Transaction Signing
```

### Key Management Providers

The system supports multiple key management backends:

1. **Environment Variable** (Recommended for production)
   - Simple, secure, widely supported
   - Works with all cloud platforms
   - Easy to rotate
   - Cost: $0 (uses platform secrets management)

2. **Azure Key Vault** (Enterprise option)
   - Managed service with automatic key rotation
   - FIPS 140-2 Level 2 validated HSMs
   - Audit logging and access controls
   - Cost: ~$0.03/10,000 operations (~$30-50/month)

3. **AWS KMS** (Enterprise option)
   - Managed service with automatic key rotation
   - FIPS 140-2 Level 2 validated HSMs  
   - CloudTrail integration for audit
   - Cost: $1/key/month + $0.03/10,000 operations

4. **Hardcoded** (Development only - NEVER use in production)
   - For local development and testing only
   - Generates security warnings in logs
   - Not secure for production use

## Production Configuration

### Option 1: Environment Variable (Recommended)

This is the recommended approach for most deployments due to its simplicity, security, and zero cost.

#### Configuration

In `appsettings.json` or `appsettings.Production.json`:

```json
{
  "KeyManagementConfig": {
    "Provider": "EnvironmentVariable",
    "EnvironmentVariableName": "BIATEC_ENCRYPTION_KEY"
  }
}
```

#### Generate Encryption Key

```bash
# Generate a secure 64-character encryption key
openssl rand -base64 48

# Or using PowerShell
$key = [Convert]::ToBase64String((1..48 | ForEach-Object { Get-Random -Maximum 256 }))
Write-Output $key
```

#### Set Environment Variable

**Docker:**
```bash
docker run -e BIATEC_ENCRYPTION_KEY="your-base64-encoded-key" biatec-tokens-api
```

**Kubernetes:**
```yaml
apiVersion: v1
kind: Secret
metadata:
  name: biatec-secrets
type: Opaque
data:
  encryption-key: <base64-encoded-key>
---
apiVersion: apps/v1
kind: Deployment
spec:
  template:
    spec:
      containers:
      - name: api
        env:
        - name: BIATEC_ENCRYPTION_KEY
          valueFrom:
            secretKeyRef:
              name: biatec-secrets
              key: encryption-key
```

**Azure App Service:**
```bash
az webapp config appsettings set \
  --resource-group myResourceGroup \
  --name myapp \
  --settings BIATEC_ENCRYPTION_KEY="your-base64-encoded-key"
```

**AWS Elastic Beanstalk:**
```bash
aws elasticbeanstalk update-environment \
  --environment-name myenv \
  --option-settings Namespace=aws:elasticbeanstalk:application:environment,OptionName=BIATEC_ENCRYPTION_KEY,Value="your-base64-encoded-key"
```

### Option 2: Azure Key Vault (Enterprise)

Azure Key Vault provides enterprise-grade key management with automatic rotation and audit logging.

#### Prerequisites

1. Create an Azure Key Vault:
```bash
az keyvault create \
  --name mykeyvault \
  --resource-group myResourceGroup \
  --location eastus
```

2. Store the encryption key:
```bash
az keyvault secret set \
  --vault-name mykeyvault \
  --name biatec-encryption-key \
  --value "your-base64-encoded-key"
```

3. Enable Managed Identity for your App Service or AKS cluster

#### Configuration

In `appsettings.Production.json`:

```json
{
  "KeyManagementConfig": {
    "Provider": "AzureKeyVault",
    "AzureKeyVault": {
      "VaultUrl": "https://mykeyvault.vault.azure.net/",
      "SecretName": "biatec-encryption-key",
      "UseManagedIdentity": true
    }
  }
}
```

#### Implementation Required

The Azure Key Vault provider is currently a stub. To enable it:

1. Install the Azure SDK:
```bash
dotnet add package Azure.Security.KeyVault.Secrets
dotnet add package Azure.Identity
```

2. Uncomment the implementation in `BiatecTokensApi/Services/AzureKeyVaultProvider.cs`

3. Update the code to use `SecretClient`:
```csharp
var credential = _config.AzureKeyVault.UseManagedIdentity 
    ? new DefaultAzureCredential() 
    : new ClientSecretCredential(
        _config.AzureKeyVault.TenantId, 
        _config.AzureKeyVault.ClientId, 
        _config.AzureKeyVault.ClientSecret);

var client = new SecretClient(
    new Uri(_config.AzureKeyVault.VaultUrl), 
    credential);

var secret = await client.GetSecretAsync(_config.AzureKeyVault.SecretName);
return secret.Value.Value;
```

### Option 3: AWS Secrets Manager (Enterprise)

AWS Secrets Manager provides enterprise-grade key management integrated with AWS CloudTrail.

#### Prerequisites

1. Create a secret in AWS Secrets Manager:
```bash
aws secretsmanager create-secret \
  --name biatec/encryption-key \
  --description "Biatec Tokens API encryption key" \
  --secret-string "your-base64-encoded-key"
```

2. Attach IAM policy to your EC2 instance role or ECS task role:
```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "secretsmanager:GetSecretValue"
      ],
      "Resource": "arn:aws:secretsmanager:us-east-1:123456789012:secret:biatec/encryption-key-*"
    }
  ]
}
```

#### Configuration

In `appsettings.Production.json`:

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

#### Implementation Required

The AWS Secrets Manager provider is currently a stub. To enable it:

1. Install the AWS SDK:
```bash
dotnet add package AWSSDK.SecretsManager
dotnet add package AWSSDK.Core
```

2. Uncomment the implementation in `BiatecTokensApi/Services/AwsKmsProvider.cs`

3. Update the code to use `AmazonSecretsManagerClient`:
```csharp
var config = new AmazonSecretsManagerConfig 
{ 
    RegionEndpoint = RegionEndpoint.GetBySystemName(_config.AwsKms.Region) 
};

var client = _config.AwsKms.UseIamRole 
    ? new AmazonSecretsManagerClient(config)
    : new AmazonSecretsManagerClient(
        _config.AwsKms.AccessKeyId,
        _config.AwsKms.SecretAccessKey,
        config);

var request = new GetSecretValueRequest 
{ 
    SecretId = _config.AwsKms.KeyId 
};

var response = await client.GetSecretValueAsync(request);
return response.SecretString;
```

## Development Configuration

For local development, you can use either the Hardcoded provider or Environment Variable.

### Hardcoded Provider (Not Secure)

In `appsettings.Development.json`:

```json
{
  "KeyManagementConfig": {
    "Provider": "Hardcoded",
    "HardcodedKey": "DevKeyForLocalTestingOnly_32CharactersMinimum_NotForProduction"
  }
}
```

**Warning:** This will generate security warnings in logs. Never use in production.

### Environment Variable (Recommended for Development)

```bash
# Set for current terminal session
export BIATEC_ENCRYPTION_KEY="DevKeyForLocalTestingOnly_32CharactersMinimum"

# Or use .NET User Secrets
dotnet user-secrets set "KeyManagementConfig:HardcodedKey" "DevKeyForLocalTestingOnly_32CharactersMinimum"
```

## Key Rotation

### Best Practices

1. **Rotation Schedule**: Rotate encryption keys at least annually
2. **Migration Strategy**: Must decrypt all existing mnemonics with old key and re-encrypt with new key
3. **Downtime**: Plan for brief maintenance window during rotation
4. **Rollback Plan**: Keep old key accessible for 30 days after rotation

### Rotation Process

1. Generate new encryption key
2. Configure dual-key support (implement `IKeyProvider` with version support)
3. Decrypt and re-encrypt all user mnemonics
4. Verify all mnemonics can be decrypted with new key
5. Update production configuration
6. Remove old key after verification period

**Note:** Key rotation with re-encryption is not currently implemented. This would require:
- Versioned key provider interface
- Migration script to re-encrypt all mnemonics
- Zero-downtime deployment strategy

## Security Best Practices

### Key Requirements

- **Minimum Length**: 32 characters (256 bits)
- **Randomness**: Use cryptographically secure random generator
- **Encoding**: Base64 encoding recommended
- **Storage**: Never commit keys to source control

### Access Control

1. **Principle of Least Privilege**: Only the API should have access to the encryption key
2. **Audit Logging**: Enable audit logs for all key access (Azure Key Vault / AWS CloudTrail)
3. **Network Security**: Use private endpoints for Key Vault / KMS access
4. **Identity Management**: Use Managed Identity / IAM Roles, not access keys

### Monitoring

Monitor these metrics:
- Key access frequency
- Failed key retrieval attempts
- Decryption errors
- Key rotation age

### Compliance

- **GDPR**: Encryption at rest for personal data
- **PCI DSS**: Key management for payment card data
- **SOC 2**: Documented key management procedures
- **HIPAA**: Encryption required for protected health information

## Validation

### Pre-Deployment Checklist

- [ ] Encryption key is at least 32 characters
- [ ] Key is generated using cryptographically secure random generator
- [ ] Key is stored in secure secrets management (not in code or plain text)
- [ ] Access to key is restricted to API service only
- [ ] Audit logging is enabled for key access
- [ ] Key rotation schedule is documented
- [ ] Backup and recovery procedures are tested
- [ ] Monitoring alerts are configured

### Validation Script

```bash
# Test key provider configuration
dotnet run --project BiatecTokensApi/BiatecTokensApi.csproj --verify-key-provider

# Or use health check endpoint
curl https://your-api.example.com/health
```

### Expected Output

```
Key Provider: EnvironmentVariable
Configuration: Valid
Key Length: 64 characters
Status: Ready
```

## Troubleshooting

### Error: "Encryption key not found in environment variable"

**Solution**: Ensure the environment variable is set correctly:
```bash
echo $BIATEC_ENCRYPTION_KEY
```

### Error: "Encryption key must be at least 32 characters long"

**Solution**: Generate a new key with sufficient length:
```bash
openssl rand -base64 48
```

### Error: "Failed to retrieve encryption key from Azure Key Vault"

**Solution**: Check:
1. Managed Identity is enabled
2. Managed Identity has "Get" permission on secrets
3. Key Vault firewall allows the service
4. Secret name matches configuration

### Error: "AWS KMS provider requires AWSSDK package"

**Solution**: The AWS Secrets Manager provider needs to be implemented. Follow the implementation instructions above.

## Cost Analysis

### Environment Variable
- **Cost**: $0
- **Setup Time**: 5 minutes
- **Operational Overhead**: Low
- **Security Level**: High (when using platform secrets)

### Azure Key Vault
- **Cost**: ~$30-50/month
- **Setup Time**: 30 minutes
- **Operational Overhead**: Low (managed service)
- **Security Level**: Very High (FIPS 140-2)

### AWS KMS
- **Cost**: ~$1-2/month base + usage
- **Setup Time**: 30 minutes
- **Operational Overhead**: Low (managed service)
- **Security Level**: Very High (FIPS 140-2)

Note: "AwsKms" provider name refers to AWS Secrets Manager service (not KMS encryption), which is the recommended AWS service for storing and retrieving application secrets.

## Migration from MVP

If you're migrating from the MVP with hardcoded `SYSTEM_KEY_FOR_MVP_REPLACE_IN_PRODUCTION`:

1. **Before going to production**: Set up Environment Variable provider
2. **Generate new key**: Use OpenSSL or PowerShell commands above
3. **Important**: All users registered during MVP will need to be re-registered
   - Existing encrypted mnemonics cannot be decrypted with new key
   - Or: Temporarily set `HardcodedKey` to MVP value to decrypt, then re-encrypt with new key

## Support

For questions or issues:
- Create GitHub issue: https://github.com/scholtz/BiatecTokensApi/issues
- Check documentation: /docs
- Review security advisories: /SECURITY.md

## References

- [Azure Key Vault Documentation](https://docs.microsoft.com/azure/key-vault/)
- [AWS KMS Documentation](https://docs.aws.amazon.com/kms/)
- [NIST Key Management Guidelines](https://csrc.nist.gov/publications/detail/sp/800-57-part-1/rev-5/final)
- [OWASP Key Management Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Key_Management_Cheat_Sheet.html)

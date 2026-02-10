# Key Management System - Production Deployment Guide

## Overview

The BiatecTokensApi uses a flexible, enterprise-grade key management system to secure user mnemonics (private keys) at rest. This guide explains how to configure and deploy the key management system for production environments with **fully implemented Azure Key Vault and AWS Secrets Manager** support.

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

The system supports four key management backends:

1. **Azure Key Vault** ✅ **FULLY IMPLEMENTED - Production Ready**
   - Managed HSM-backed secret storage
   - FIPS 140-2 Level 2 validated HSMs
   - Audit logging and access controls
   - Managed identity support
   - Automatic retry and failover
   - Cost: ~$0.03/10,000 operations (~$30-50/month)

2. **AWS Secrets Manager** ✅ **FULLY IMPLEMENTED - Production Ready**
   - Managed secret storage with encryption at rest
   - FIPS 140-2 Level 2 validated HSMs
   - IAM role support for secure authentication
   - CloudTrail integration for audit
   - Cost: $0.40/secret/month + $0.05/10,000 API calls

3. **Environment Variable** (Lightweight production option)
   - Simple, secure, widely supported
   - Works with all cloud platforms
   - Easy to rotate with orchestration tools
   - Cost: $0 (uses platform secrets management)
   - **Blocked in non-development environments by health check**

4. **Hardcoded** (Development only - NEVER use in production)
   - For local development and testing only
   - Generates security warnings in logs
   - **Blocked in non-development environments by health check**

## Production Safeguards

### Automatic Environment Detection

The system includes a **KeyManagementHealthCheck** that:
- ✅ Validates provider configuration on startup
- ✅ **BLOCKS application startup** if insecure providers (Environment Variable, Hardcoded) are used in production
- ✅ Tests KMS connectivity before accepting traffic
- ✅ Provides explicit error codes for troubleshooting
- ✅ Logs all key operations with correlation IDs for audit

### Error Codes

- `KMS_INSECURE_PROVIDER_IN_PRODUCTION` - Insecure provider detected in production
- `KMS_INVALID_CONFIGURATION` - Provider configuration is invalid
- `KMS_INVALID_KEY` - Retrieved key is invalid or too short
- `KMS_CONNECTIVITY_FAILED` - Cannot connect to KMS
- `KMS_AZURE_RETRIEVAL_FAILED` - Azure Key Vault retrieval failed
- `KMS_AWS_RETRIEVAL_FAILED` - AWS Secrets Manager retrieval failed

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

✅ **FULLY IMPLEMENTED** - Ready for production use!

#### Managed Identity Setup (Recommended)

1. **Enable managed identity** on your Azure resource:

**App Service:**
```bash
az webapp identity assign \
  --resource-group myResourceGroup \
  --name myapp
```

**AKS (Kubernetes):**
```bash
# Use Azure AD Workload Identity
az aks update \
  --resource-group myResourceGroup \
  --name myakscluster \
  --enable-oidc-issuer \
  --enable-workload-identity
```

2. **Grant access** to the managed identity:
```bash
# Get the managed identity principal ID
IDENTITY_ID=$(az webapp identity show \
  --resource-group myResourceGroup \
  --name myapp \
  --query principalId -o tsv)

# Grant access to the secret
az keyvault set-policy \
  --name mykeyvault \
  --object-id $IDENTITY_ID \
  --secret-permissions get
```

3. **Test access**:
```bash
# Verify the identity can access the secret
az keyvault secret show \
  --vault-name mykeyvault \
  --name biatec-encryption-key
```

#### Service Principal Setup (Alternative)

If managed identity is not available:

1. **Create a service principal**:
```bash
az ad sp create-for-rbac --name biatec-tokens-api
# Save the output: appId, password, tenant
```

2. **Grant access to Key Vault**:
```bash
az keyvault set-policy \
  --name mykeyvault \
  --spn <appId> \
  --secret-permissions get
```

3. **Configure with credentials**:
```json
{
  "KeyManagementConfig": {
    "Provider": "AzureKeyVault",
    "AzureKeyVault": {
      "VaultUrl": "https://mykeyvault.vault.azure.net/",
      "SecretName": "biatec-encryption-key",
      "UseManagedIdentity": false,
      "TenantId": "<tenant-id>",
      "ClientId": "<app-id>",
      "ClientSecret": "<password>"
    }
  }
}
```

⚠️ **Security Note**: Never commit credentials to source control. Use environment variables or Azure Key Vault references for ClientSecret.

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

✅ **FULLY IMPLEMENTED** - Ready for production use!

#### IAM Role Setup (Recommended)

1. **Create IAM policy**:
```bash
# Create policy document
cat > secrets-policy.json <<EOF
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "secretsmanager:GetSecretValue",
        "secretsmanager:DescribeSecret"
      ],
      "Resource": "arn:aws:secretsmanager:us-east-1:123456789012:secret:biatec/encryption-key-*"
    }
  ]
}
EOF

# Create the policy
aws iam create-policy \
  --policy-name BiatecTokensApiSecretsAccess \
  --policy-document file://secrets-policy.json
```

2. **Attach to IAM role**:

**EC2 Instance:**
```bash
aws iam attach-role-policy \
  --role-name my-ec2-role \
  --policy-arn arn:aws:iam::123456789012:policy/BiatecTokensApiSecretsAccess
```

**ECS Task:**
```bash
aws iam attach-role-policy \
  --role-name my-ecs-task-role \
  --policy-arn arn:aws:iam::123456789012:policy/BiatecTokensApiSecretsAccess
```

**EKS Pod (IRSA):**
```bash
eksctl create iamserviceaccount \
  --name biatec-tokens-api \
  --namespace default \
  --cluster my-cluster \
  --attach-policy-arn arn:aws:iam::123456789012:policy/BiatecTokensApiSecretsAccess \
  --approve
```

3. **Test access**:
```bash
# Verify IAM role can access the secret
aws secretsmanager get-secret-value \
  --secret-id biatec/encryption-key \
  --query SecretString \
  --output text
```

#### Access Key Setup (Alternative)

If IAM roles are not available:

1. **Create IAM user**:
```bash
aws iam create-user --user-name biatec-tokens-api
aws iam attach-user-policy \
  --user-name biatec-tokens-api \
  --policy-arn arn:aws:iam::123456789012:policy/BiatecTokensApiSecretsAccess
aws iam create-access-key --user-name biatec-tokens-api
# Save AccessKeyId and SecretAccessKey
```

2. **Configure with credentials**:
```json
{
  "KeyManagementConfig": {
    "Provider": "AwsKms",
    "AwsKms": {
      "Region": "us-east-1",
      "KeyId": "biatec/encryption-key",
      "UseIamRole": false,
      "AccessKeyId": "<access-key-id>",
      "SecretAccessKey": "<secret-access-key>"
    }
  }
}
```

⚠️ **Security Note**: Never commit credentials to source control. Use environment variables or AWS Secrets Manager for AccessKeyId and SecretAccessKey.

### Option 4: Environment Variable (Lightweight Production)

⚠️ **Note**: Environment Variable provider is automatically **blocked in non-development environments** by the health check. To use in production, you must explicitly set `ASPNETCORE_ENVIRONMENT=Production` and accept the security implications.

For complete setup instructions, see Option 1 in the production configuration section above.

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

1. **Rotation Schedule**: Rotate encryption keys at least annually (recommended: quarterly for high-security environments)
2. **Migration Strategy**: Must decrypt all existing mnemonics with old key and re-encrypt with new key
3. **Downtime**: Plan for brief maintenance window during rotation (5-15 minutes for most deployments)
4. **Rollback Plan**: Keep old key accessible for 30 days after rotation
5. **Testing**: Always test rotation procedure in staging environment first
6. **Audit**: Log all rotation operations with timestamps and operator identity

### Azure Key Vault Rotation

#### Step 1: Create New Secret Version

```bash
# Generate new encryption key
NEW_KEY=$(openssl rand -base64 48)

# Create new version of the secret (Azure automatically versions secrets)
az keyvault secret set \
  --vault-name mykeyvault \
  --name biatec-encryption-key \
  --value "$NEW_KEY"

# Verify new version exists
az keyvault secret show \
  --vault-name mykeyvault \
  --name biatec-encryption-key \
  --query "id"
```

#### Step 2: Enable Dual-Key Support (Temporary)

Update application configuration to support both keys during migration:

```json
{
  "KeyManagementConfig": {
    "Provider": "AzureKeyVault",
    "AzureKeyVault": {
      "VaultUrl": "https://mykeyvault.vault.azure.net/",
      "SecretName": "biatec-encryption-key",
      "UseManagedIdentity": true
    },
    "LegacyKeyVault": {
      "VaultUrl": "https://mykeyvault.vault.azure.net/",
      "SecretName": "biatec-encryption-key-old",
      "UseManagedIdentity": true
    }
  }
}
```

#### Step 3: Re-encrypt Data (Maintenance Window)

```bash
# Run re-encryption script (to be implemented)
# This would:
# 1. Stop accepting new registrations
# 2. Decrypt all mnemonics with old key
# 3. Re-encrypt with new key
# 4. Verify all records
# 5. Resume normal operations

# Example pseudo-code:
# foreach user in database:
#     old_mnemonic = decrypt(user.EncryptedMnemonic, OLD_KEY)
#     new_encrypted = encrypt(old_mnemonic, NEW_KEY)
#     update_database(user.id, new_encrypted)
#     verify_decrypt(new_encrypted, NEW_KEY)
```

#### Step 4: Verify and Remove Old Key

```bash
# After 30-day verification period
az keyvault secret delete \
  --vault-name mykeyvault \
  --name biatec-encryption-key-old

# Purge after retention period
az keyvault secret purge \
  --vault-name mykeyvault \
  --name biatec-encryption-key-old
```

### AWS Secrets Manager Rotation

#### Step 1: Create New Secret

```bash
# Generate new encryption key
NEW_KEY=$(openssl rand -base64 48)

# Store current key version ID for reference
CURRENT_VERSION=$(aws secretsmanager describe-secret \
  --secret-id biatec/encryption-key \
  --query 'VersionIdsToStages' \
  --output json | jq -r 'to_entries[] | select(.value[] == "AWSCURRENT") | .key')

# Create new secret version (automatically gets AWSCURRENT stage)
aws secretsmanager put-secret-value \
  --secret-id biatec/encryption-key \
  --secret-string "$NEW_KEY"

# Previous version is automatically available via version ID
# (AWS does not automatically assign AWSPREVIOUS stage)
echo "Previous version ID: $CURRENT_VERSION"
echo "Store this for potential rollback"
```

#### Step 2: Configure Rotation Lambda (Optional)

AWS Secrets Manager supports automatic rotation:

```bash
# Create rotation Lambda function
aws lambda create-function \
  --function-name BiatecKeyRotation \
  --runtime python3.11 \
  --role arn:aws:iam::123456789012:role/lambda-execution-role \
  --handler rotation.lambda_handler \
  --zip-file fileb://rotation.zip

# Enable automatic rotation
aws secretsmanager rotate-secret \
  --secret-id biatec/encryption-key \
  --rotation-lambda-arn arn:aws:lambda:us-east-1:123456789012:function:BiatecKeyRotation \
  --rotation-rules AutomaticallyAfterDays=90
```

#### Step 3: Re-encrypt Data

Follow the same re-encryption process as Azure (Step 3 above).

#### Step 4: Verify and Delete Old Secret

```bash
# After 30-day verification period
aws secretsmanager delete-secret \
  --secret-id biatec/encryption-key-old \
  --recovery-window-in-days 30

# Force delete after recovery window
aws secretsmanager delete-secret \
  --secret-id biatec/encryption-key-old \
  --force-delete-without-recovery
```

### Rollback Procedure

If issues are detected after rotation:

#### Azure Key Vault

```bash
# Retrieve previous version
OLD_VERSION_ID=$(az keyvault secret list-versions \
  --vault-name mykeyvault \
  --name biatec-encryption-key \
  --query "[1].id" -o tsv)

# Restore previous version as current
az keyvault secret set \
  --vault-name mykeyvault \
  --name biatec-encryption-key \
  --value "$(az keyvault secret show --id $OLD_VERSION_ID --query value -o tsv)"
```

#### AWS Secrets Manager

```bash
# Step 1: Get current and previous version IDs
CURRENT_VERSION=$(aws secretsmanager describe-secret \
  --secret-id biatec/encryption-key \
  --query 'VersionIdsToStages' \
  --output json | jq -r 'to_entries[] | select(.value[] == "AWSCURRENT") | .key')

PREVIOUS_VERSION=$(aws secretsmanager list-secret-version-ids \
  --secret-id biatec/encryption-key \
  --include-deprecated \
  --query 'Versions[1].VersionId' \
  --output text)

# Step 2: Revert to previous version
aws secretsmanager update-secret-version-stage \
  --secret-id biatec/encryption-key \
  --version-stage AWSCURRENT \
  --remove-from-version-id "$CURRENT_VERSION" \
  --move-to-version-id "$PREVIOUS_VERSION"

# Step 3: Verify rollback
aws secretsmanager get-secret-value \
  --secret-id biatec/encryption-key \
  --version-stage AWSCURRENT
```

### Future Enhancement: Automated Re-encryption

**Note:** Automated re-encryption with zero downtime is planned for a future release. This would require:

1. **Versioned Key Provider Interface**: Support multiple key versions simultaneously
2. **Background Migration Job**: Re-encrypt mnemonics in batches without downtime
3. **Migration Progress Tracking**: Database table to track re-encryption status
4. **Graceful Degradation**: Fall back to old key if new key fails
5. **Health Checks**: Monitor re-encryption progress and errors

**Estimated Implementation**: 2-3 weeks development + testing

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

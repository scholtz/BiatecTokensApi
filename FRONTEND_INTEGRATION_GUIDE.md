# Frontend Integration Guide

## Overview

This guide provides comprehensive information for frontend developers integrating with the BiatecTokensApi. It covers authentication, error handling, idempotency, subscription management, and deployment tracking.

## Table of Contents

1. [Authentication](#authentication)
2. [Error Handling](#error-handling)
3. [Idempotency](#idempotency)
4. [Subscription Management](#subscription-management)
5. [Token Deployment](#token-deployment)
6. [Health Monitoring](#health-monitoring)
7. [Best Practices](#best-practices)

---

## Authentication

### ARC-0014 Authentication

The API uses ARC-0014 (Algorand Authentication) for all protected endpoints. This is transaction-based authentication where the user signs an authentication transaction.

#### How It Works

1. User wants to access a protected endpoint
2. Frontend creates an authentication transaction
3. User signs the transaction with their wallet
4. Frontend includes the signed transaction in the Authorization header
5. API validates the signature and extracts the user's address

#### Implementation Example

```typescript
// Example using AlgoSDK
import algosdk from 'algosdk';

async function authenticateUser(userAccount: algosdk.Account): Promise<string> {
  const algodClient = new algosdk.Algodv2(token, server, port);
  
  // Get suggested params
  const params = await algodClient.getTransactionParams().do();
  
  // Create authentication transaction
  const authTxn = algosdk.makePaymentTxnWithSuggestedParamsFromObject({
    from: userAccount.addr,
    to: userAccount.addr,
    amount: 0,
    note: new Uint8Array(Buffer.from('BiatecTokens#ARC14')),
    suggestedParams: params
  });
  
  // Sign transaction
  const signedTxn = authTxn.signTxn(userAccount.sk);
  
  // Encode for Authorization header
  const authHeader = `SigTx ${Buffer.from(signedTxn).toString('base64')}`;
  
  return authHeader;
}

// Use in API calls
const authHeader = await authenticateUser(userAccount);
const response = await fetch('https://api.example.com/api/v1/subscription/status', {
  headers: {
    'Authorization': authHeader
  }
});
```

#### Authorization Header Format

```
Authorization: SigTx <base64-encoded-signed-transaction>
```

#### Protected Endpoints

All endpoints require authentication except:
- `GET /health`, `/health/ready`, `/health/live`
- `GET /api/v1/status`
- `POST /api/v1/subscription/webhook` (Stripe webhooks use signature validation)

---

## Error Handling

### Error Response Structure

All API errors return a consistent JSON structure:

```json
{
  "success": false,
  "errorCode": "ERROR_CODE",
  "errorMessage": "Human-readable error description",
  "remediationHint": "Suggestion to help resolve the error",
  "details": {
    "additionalInfo": "Optional debugging information"
  },
  "timestamp": "2026-02-03T12:00:00Z",
  "path": "/api/v1/token/erc20-mintable/create",
  "correlationId": "abc-123-def-456"
}
```

### Error Codes

#### Validation Errors (400)

| Error Code | Description | Remediation Hint |
|------------|-------------|------------------|
| `INVALID_REQUEST` | Invalid request parameters | Check your request parameters and ensure all required fields are provided with valid values |
| `MISSING_REQUIRED_FIELD` | Required field is missing | Review the API documentation for required fields |
| `INVALID_NETWORK` | Blockchain network is invalid | Use a supported network name (mainnet, testnet, etc.) |
| `INVALID_TOKEN_PARAMETERS` | Token parameters are invalid | Verify token decimals, supply, and name meet requirements |

#### Authentication Errors (401, 403)

| Error Code | Description | Remediation Hint |
|------------|-------------|------------------|
| `UNAUTHORIZED` | Authentication required | Provide a valid ARC-0014 authentication token in the Authorization header |
| `FORBIDDEN` | Insufficient permissions | Check account permissions and subscription tier |
| `INVALID_AUTH_TOKEN` | Auth token invalid/expired | Refresh authentication token |

#### Blockchain Errors (422, 502)

| Error Code | Description | Remediation Hint |
|------------|-------------|------------------|
| `BLOCKCHAIN_CONNECTION_ERROR` | Cannot connect to blockchain | Check network status and availability. If problem persists, contact support |
| `TRANSACTION_FAILED` | Transaction failed on blockchain | Verify account balance and transaction parameters, then try again |
| `CONTRACT_EXECUTION_FAILED` | Smart contract execution failed | Check contract parameters |
| `INSUFFICIENT_FUNDS` | Account has insufficient funds | Add funds to account |
| `TRANSACTION_REJECTED` | Network rejected transaction | Check transaction validity |

#### Service Errors (500, 503, 504)

| Error Code | Description | Remediation Hint |
|------------|-------------|------------------|
| `INTERNAL_SERVER_ERROR` | Internal server error | An unexpected error occurred. If this persists, please contact support with the correlation ID |
| `IPFS_SERVICE_ERROR` | IPFS service unavailable | IPFS service is temporarily unavailable. Please try again in a few moments |
| `EXTERNAL_SERVICE_ERROR` | External service error | The external service is currently unavailable. Please retry in a few moments |
| `TIMEOUT` | Request timeout | The operation took too long to complete. Wait a moment and retry your request |

#### Rate Limiting (429)

| Error Code | Description | Remediation Hint |
|------------|-------------|------------------|
| `RATE_LIMIT_EXCEEDED` | Rate limit exceeded | Too many requests. Please wait and try again |
| `SUBSCRIPTION_LIMIT_REACHED` | Subscription limit reached | Upgrade your subscription plan to continue |

### TypeScript Error Handler Example

```typescript
interface ApiError {
  success: false;
  errorCode: string;
  errorMessage: string;
  remediationHint?: string;
  details?: Record<string, any>;
  timestamp: string;
  path?: string;
  correlationId?: string;
}

async function handleApiCall<T>(apiCall: () => Promise<Response>): Promise<T> {
  try {
    const response = await apiCall();
    
    if (!response.ok) {
      const error: ApiError = await response.json();
      
      // Display user-friendly message
      console.error(`Error: ${error.errorMessage}`);
      
      // Show remediation hint if available
      if (error.remediationHint) {
        console.log(`Suggestion: ${error.remediationHint}`);
      }
      
      // Log correlation ID for support
      if (error.correlationId) {
        console.log(`Reference ID: ${error.correlationId}`);
      }
      
      throw new Error(error.errorMessage);
    }
    
    return await response.json();
  } catch (error) {
    // Handle network errors
    if (error instanceof TypeError) {
      throw new Error('Network error. Please check your connection.');
    }
    throw error;
  }
}

// Usage
try {
  const result = await handleApiCall(() => 
    fetch('https://api.example.com/api/v1/token/erc20-mintable/create', {
      method: 'POST',
      headers: {
        'Authorization': authHeader,
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(tokenRequest)
    })
  );
  console.log('Success:', result);
} catch (error) {
  // Display error to user
  showErrorMessage(error.message);
}
```

---

## Idempotency

### Overview

All token deployment endpoints support idempotency to prevent accidental duplicate deployments. This is especially important when network issues might cause request retries.

### How It Works

1. Client generates a unique idempotency key (e.g., UUID)
2. Client includes the key in the `Idempotency-Key` header
3. API caches the response for 24 hours
4. Subsequent requests with the same key return the cached response
5. Response includes `X-Idempotency-Hit` header indicating cache hit/miss

### When to Use Idempotency

**Always use for:**
- Token deployments (all create endpoints)
- Any operation with financial implications
- Operations that should not be repeated

**Optional for:**
- Read operations (GET requests)
- Status checks

### Implementation Example

```typescript
import { v4 as uuidv4 } from 'uuid';

interface DeploymentRequest {
  idempotencyKey: string;
  request: any;
}

class TokenDeploymentService {
  private pendingDeployments: Map<string, string> = new Map();
  
  async deployToken(request: any): Promise<any> {
    // Generate unique idempotency key
    const idempotencyKey = uuidv4();
    
    // Store for retry logic
    this.pendingDeployments.set(idempotencyKey, JSON.stringify(request));
    
    try {
      const response = await fetch('https://api.example.com/api/v1/token/erc20-mintable/create', {
        method: 'POST',
        headers: {
          'Authorization': authHeader,
          'Content-Type': 'application/json',
          'Idempotency-Key': idempotencyKey
        },
        body: JSON.stringify(request)
      });
      
      const result = await response.json();
      const cacheHit = response.headers.get('X-Idempotency-Hit') === 'true';
      
      if (cacheHit) {
        console.log('Returned cached response (duplicate request detected)');
      }
      
      // Clean up on success
      this.pendingDeployments.delete(idempotencyKey);
      
      return result;
    } catch (error) {
      // Keep key for retry
      console.error('Deployment failed, can retry with same key:', idempotencyKey);
      throw error;
    }
  }
  
  async retryDeployment(idempotencyKey: string): Promise<any> {
    const requestData = this.pendingDeployments.get(idempotencyKey);
    if (!requestData) {
      throw new Error('No pending deployment found for this key');
    }
    
    // Retry with same idempotency key
    const response = await fetch('https://api.example.com/api/v1/token/erc20-mintable/create', {
      method: 'POST',
      headers: {
        'Authorization': await getAuthHeader(),
        'Content-Type': 'application/json',
        'Idempotency-Key': idempotencyKey
      },
      body: requestData
    });
    
    return await response.json();
  }
}
```

### Idempotency Key Generation Best Practices

```typescript
// Option 1: UUID v4 (recommended)
import { v4 as uuidv4 } from 'uuid';
const idempotencyKey = uuidv4();

// Option 2: Timestamp + random string
const idempotencyKey = `${Date.now()}-${Math.random().toString(36).substring(2, 11)}`;

// Option 3: Hash of request (for deterministic keys)
import { createHash } from 'crypto';
const idempotencyKey = createHash('sha256')
  .update(JSON.stringify(request))
  .digest('hex');
```

### Supported Endpoints

All token deployment endpoints support idempotency:
- `POST /api/v1/token/erc20-mintable/create`
- `POST /api/v1/token/erc20-preminted/create`
- `POST /api/v1/token/asa-ft/create`
- `POST /api/v1/token/asa-nft/create`
- `POST /api/v1/token/asa-fnft/create`
- `POST /api/v1/token/arc3-ft/create`
- `POST /api/v1/token/arc3-nft/create`
- `POST /api/v1/token/arc3-fnft/create`
- `POST /api/v1/token/arc200-mintable/create`
- `POST /api/v1/token/arc200-preminted/create`
- `POST /api/v1/token/arc1400-mintable/create`

---

## Subscription Management

### Overview

The API uses Stripe for subscription management with four tiers: Free, Basic, Premium, and Enterprise. Each tier has different limits and features.

### Subscription Workflow

```
1. User clicks "Upgrade" → Frontend calls CreateCheckoutSession
2. User redirected to Stripe → Completes payment
3. Stripe redirects back → User returns to success URL
4. Stripe sends webhook → Backend updates subscription state
5. Frontend polls status → Confirms activation
```

### Create Checkout Session

```typescript
interface CheckoutSessionRequest {
  tier: 'basic' | 'premium' | 'enterprise';
}

interface CheckoutSessionResponse {
  success: boolean;
  checkoutUrl?: string;
  sessionId?: string;
  errorMessage?: string;
}

async function createCheckoutSession(tier: string): Promise<string> {
  const response = await fetch('https://api.example.com/api/v1/subscription/checkout', {
    method: 'POST',
    headers: {
      'Authorization': await getAuthHeader(),
      'Content-Type': 'application/json'
    },
    body: JSON.stringify({ tier })
  });
  
  const result: CheckoutSessionResponse = await response.json();
  
  if (result.success && result.checkoutUrl) {
    return result.checkoutUrl;
  }
  
  throw new Error(result.errorMessage || 'Failed to create checkout session');
}

// Usage
const checkoutUrl = await createCheckoutSession('premium');
window.location.href = checkoutUrl; // Redirect to Stripe
```

### Get Subscription Status

```typescript
interface SubscriptionStatus {
  userAddress: string;
  stripeCustomerId?: string;
  stripeSubscriptionId?: string;
  tier: 'free' | 'basic' | 'premium' | 'enterprise';
  status: 'none' | 'active' | 'past_due' | 'canceled' | 'incomplete' | 'trialing';
  subscriptionStartDate?: string;
  currentPeriodStart?: string;
  currentPeriodEnd?: string;
  cancelAtPeriodEnd: boolean;
  lastUpdated: string;
}

interface SubscriptionStatusResponse {
  success: boolean;
  subscription?: SubscriptionStatus;
  errorMessage?: string;
}

async function getSubscriptionStatus(): Promise<SubscriptionStatus> {
  const response = await fetch('https://api.example.com/api/v1/subscription/status', {
    headers: {
      'Authorization': await getAuthHeader()
    }
  });
  
  const result: SubscriptionStatusResponse = await response.json();
  
  if (result.success && result.subscription) {
    return result.subscription;
  }
  
  throw new Error(result.errorMessage || 'Failed to get subscription status');
}

// Usage example
const status = await getSubscriptionStatus();
console.log(`Current tier: ${status.tier}`);
console.log(`Status: ${status.status}`);
if (status.currentPeriodEnd) {
  console.log(`Renews on: ${new Date(status.currentPeriodEnd).toLocaleDateString()}`);
}
```

### Billing Portal

```typescript
interface BillingPortalRequest {
  returnUrl?: string;
}

interface BillingPortalResponse {
  success: boolean;
  portalUrl?: string;
  errorMessage?: string;
}

async function openBillingPortal(returnUrl?: string): Promise<string> {
  const response = await fetch('https://api.example.com/api/v1/subscription/billing-portal', {
    method: 'POST',
    headers: {
      'Authorization': await getAuthHeader(),
      'Content-Type': 'application/json'
    },
    body: JSON.stringify({ returnUrl })
  });
  
  const result: BillingPortalResponse = await response.json();
  
  if (result.success && result.portalUrl) {
    return result.portalUrl;
  }
  
  throw new Error(result.errorMessage || 'Failed to create billing portal session');
}

// Usage
const portalUrl = await openBillingPortal('https://yourapp.com/account');
window.open(portalUrl, '_blank'); // Open in new tab
```

### Plan Limits Check

```typescript
interface PlanLimits {
  maxTokenIssuance: number;
  maxTransferValidations: number;
  maxAuditExports: number;
  maxStorageItems: number;
  maxComplianceOperations: number;
  maxWhitelistOperations: number;
}

interface UsageSummary {
  userAddress: string;
  currentTier: string;
  currentPeriod: {
    start: string;
    end: string;
  };
  usage: {
    tokenIssuanceCount: number;
    transferValidationCount: number;
    auditExportCount: number;
    storageItemsCount: number;
    complianceOperationCount: number;
    whitelistOperationCount: number;
  };
  limits: PlanLimits;
  violations: string[];
}

async function checkUsageLimits(): Promise<UsageSummary> {
  const response = await fetch('https://api.example.com/api/v1/billing/usage', {
    headers: {
      'Authorization': await getAuthHeader()
    }
  });
  
  return await response.json();
}

// Usage - check before operations
const usage = await checkUsageLimits();
const canDeploy = usage.usage.tokenIssuanceCount < usage.limits.maxTokenIssuance;

if (!canDeploy) {
  alert('You have reached your token deployment limit. Please upgrade your plan.');
}
```

---

## Token Deployment

### Deployment Status Tracking

All token deployments return a `deploymentId` that can be used to track deployment progress in real-time.

```typescript
interface DeploymentStatus {
  deploymentId: string;
  userAddress: string;
  network: string;
  tokenType: string;
  currentStatus: 'queued' | 'submitted' | 'pending' | 'confirmed' | 'completed' | 'failed';
  statusHistory: {
    status: string;
    timestamp: string;
    message?: string;
    errorDetails?: any;
  }[];
  assetId?: string;
  transactionId?: string;
  contractAddress?: string;
  createdAt: string;
  lastUpdated: string;
}

async function trackDeployment(deploymentId: string): Promise<DeploymentStatus> {
  const response = await fetch(
    `https://api.example.com/api/v1/token/deployments/${deploymentId}`,
    {
      headers: {
        'Authorization': await getAuthHeader()
      }
    }
  );
  
  if (!response.ok) {
    throw new Error('Failed to get deployment status');
  }
  
  return await response.json();
}

// Polling example
async function pollDeploymentStatus(
  deploymentId: string,
  onUpdate: (status: DeploymentStatus) => void,
  interval: number = 2000
): Promise<DeploymentStatus> {
  return new Promise((resolve, reject) => {
    const poll = async () => {
      try {
        const status = await trackDeployment(deploymentId);
        onUpdate(status);
        
        // Check if deployment is complete
        if (status.currentStatus === 'completed') {
          resolve(status);
        } else if (status.currentStatus === 'failed') {
          reject(new Error('Deployment failed'));
        } else {
          // Continue polling
          setTimeout(poll, interval);
        }
      } catch (error) {
        reject(error);
      }
    };
    
    poll();
  });
}

// Usage
const response = await deployToken(request);
if (response.success && response.deploymentId) {
  await pollDeploymentStatus(
    response.deploymentId,
    (status) => {
      console.log(`Deployment status: ${status.currentStatus}`);
      updateUI(status);
    }
  );
}
```

### List Deployments

```typescript
interface DeploymentFilter {
  network?: string;
  status?: string;
  tokenType?: string;
  fromDate?: string;
  toDate?: string;
  page?: number;
  pageSize?: number;
}

async function listDeployments(filter: DeploymentFilter = {}): Promise<any> {
  const params = new URLSearchParams();
  Object.entries(filter).forEach(([key, value]) => {
    if (value !== undefined) {
      params.append(key, value.toString());
    }
  });
  
  const response = await fetch(
    `https://api.example.com/api/v1/token/deployments?${params}`,
    {
      headers: {
        'Authorization': await getAuthHeader()
      }
    }
  );
  
  return await response.json();
}

// Usage
const recentDeployments = await listDeployments({
  status: 'completed',
  page: 1,
  pageSize: 10
});
```

---

## Health Monitoring

### Health Check Endpoints

```typescript
// Basic health check
async function checkHealth(): Promise<boolean> {
  try {
    const response = await fetch('https://api.example.com/health');
    return response.ok;
  } catch {
    return false;
  }
}

// Detailed status check
interface ApiStatus {
  status: string;
  version: string;
  buildTime: string;
  timestamp: string;
  uptime: string;
  environment: string;
  components: Record<string, {
    status: string;
    message?: string;
  }>;
}

async function getDetailedStatus(): Promise<ApiStatus> {
  const response = await fetch('https://api.example.com/api/v1/status');
  return await response.json();
}

// Usage - display status badge
const status = await getDetailedStatus();
if (status.status === 'Healthy') {
  showGreenStatusBadge();
} else {
  showYellowStatusBadge();
}
```

---

## Best Practices

### 1. Error Handling

```typescript
// Always handle errors gracefully
try {
  const result = await apiCall();
  handleSuccess(result);
} catch (error) {
  // Check if it's an API error with remediation hint
  if (error.remediationHint) {
    showUserFriendlyError(error.errorMessage, error.remediationHint);
  } else {
    showGenericError();
  }
  
  // Log for debugging
  console.error('API Error:', {
    message: error.errorMessage,
    code: error.errorCode,
    correlationId: error.correlationId
  });
}
```

### 2. Authentication

```typescript
// Cache authentication tokens
class AuthService {
  private authToken: string | null = null;
  private tokenExpiry: number = 0;
  
  async getAuthToken(): Promise<string> {
    // Check if token is still valid (5 minute expiry)
    if (this.authToken && Date.now() < this.tokenExpiry) {
      return this.authToken;
    }
    
    // Generate new token
    this.authToken = await generateARC14Token();
    this.tokenExpiry = Date.now() + (5 * 60 * 1000);
    
    return this.authToken;
  }
}
```

### 3. Idempotency

```typescript
// Store idempotency keys for retry
const IDEMPOTENCY_STORAGE_KEY = 'pending_deployments';

function storeIdempotencyKey(key: string, request: any) {
  const pending = JSON.parse(
    localStorage.getItem(IDEMPOTENCY_STORAGE_KEY) || '{}'
  );
  pending[key] = {
    request,
    timestamp: Date.now()
  };
  localStorage.setItem(IDEMPOTENCY_STORAGE_KEY, JSON.stringify(pending));
}

function cleanupIdempotencyKey(key: string) {
  const pending = JSON.parse(
    localStorage.getItem(IDEMPOTENCY_STORAGE_KEY) || '{}'
  );
  delete pending[key];
  localStorage.setItem(IDEMPOTENCY_STORAGE_KEY, JSON.stringify(pending));
}
```

### 4. Rate Limiting

```typescript
// Implement exponential backoff for retries
async function retryWithBackoff<T>(
  operation: () => Promise<T>,
  maxRetries: number = 3
): Promise<T> {
  let lastError: Error;
  
  for (let i = 0; i < maxRetries; i++) {
    try {
      return await operation();
    } catch (error) {
      lastError = error;
      
      // Check if it's a rate limit error
      if (error.errorCode === 'RATE_LIMIT_EXCEEDED') {
        const delay = Math.pow(2, i) * 1000; // Exponential backoff
        console.log(`Rate limited, retrying in ${delay}ms...`);
        await new Promise(resolve => setTimeout(resolve, delay));
      } else {
        throw error; // Don't retry other errors
      }
    }
  }
  
  throw lastError!;
}
```

### 5. Subscription Status

```typescript
// Poll subscription status after checkout
async function waitForSubscriptionActivation(
  expectedTier: string,
  maxWaitTime: number = 60000
): Promise<boolean> {
  const startTime = Date.now();
  
  while (Date.now() - startTime < maxWaitTime) {
    const status = await getSubscriptionStatus();
    
    if (status.tier === expectedTier && status.status === 'active') {
      return true;
    }
    
    await new Promise(resolve => setTimeout(resolve, 2000));
  }
  
  return false;
}

// Usage after checkout
const checkoutUrl = await createCheckoutSession('premium');
window.location.href = checkoutUrl;

// On return from Stripe
const activated = await waitForSubscriptionActivation('premium');
if (activated) {
  showSuccessMessage('Subscription activated!');
} else {
  showInfoMessage('Subscription activation pending...');
}
```

---

## Complete React Example

```typescript
import React, { useState, useEffect } from 'react';
import { v4 as uuidv4 } from 'uuid';

interface TokenDeploymentFormProps {
  authHeader: string;
}

const TokenDeploymentForm: React.FC<TokenDeploymentFormProps> = ({ authHeader }) => {
  const [tokenName, setTokenName] = useState('');
  const [tokenSymbol, setTokenSymbol] = useState('');
  const [loading, setLoading] = useState(false);
  const [deploymentId, setDeploymentId] = useState<string | null>(null);
  const [status, setStatus] = useState<string>('');
  const [error, setError] = useState<string | null>(null);
  
  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    setError(null);
    
    const idempotencyKey = uuidv4();
    
    try {
      const response = await fetch('https://api.example.com/api/v1/token/erc20-mintable/create', {
        method: 'POST',
        headers: {
          'Authorization': authHeader,
          'Content-Type': 'application/json',
          'Idempotency-Key': idempotencyKey
        },
        body: JSON.stringify({
          name: tokenName,
          symbol: tokenSymbol,
          decimals: 18,
          initialSupply: '1000000'
        })
      });
      
      const result = await response.json();
      
      if (result.success) {
        setDeploymentId(result.deploymentId);
        // Start polling deployment status
      } else {
        setError(result.errorMessage);
        if (result.remediationHint) {
          setError(`${result.errorMessage}\n\nSuggestion: ${result.remediationHint}`);
        }
      }
    } catch (err) {
      setError('Network error. Please check your connection.');
    } finally {
      setLoading(false);
    }
  };
  
  useEffect(() => {
    if (!deploymentId) return;
    
    const pollStatus = async () => {
      try {
        const response = await fetch(
          `https://api.example.com/api/v1/token/deployments/${deploymentId}`,
          {
            headers: { 'Authorization': authHeader }
          }
        );
        
        const statusData = await response.json();
        setStatus(statusData.currentStatus);
        
        if (statusData.currentStatus === 'completed') {
          // Stop polling
          return;
        } else if (statusData.currentStatus === 'failed') {
          setError('Deployment failed');
          return;
        }
        
        // Continue polling
        setTimeout(pollStatus, 2000);
      } catch (err) {
        setError('Failed to check deployment status');
      }
    };
    
    pollStatus();
  }, [deploymentId, authHeader]);
  
  return (
    <div>
      <form onSubmit={handleSubmit}>
        <input
          type="text"
          placeholder="Token Name"
          value={tokenName}
          onChange={(e) => setTokenName(e.target.value)}
          disabled={loading}
        />
        <input
          type="text"
          placeholder="Token Symbol"
          value={tokenSymbol}
          onChange={(e) => setTokenSymbol(e.target.value)}
          disabled={loading}
        />
        <button type="submit" disabled={loading}>
          {loading ? 'Deploying...' : 'Deploy Token'}
        </button>
      </form>
      
      {error && <div className="error">{error}</div>}
      {status && <div className="status">Deployment Status: {status}</div>}
    </div>
  );
};

export default TokenDeploymentForm;
```

---

## Support

For additional support:
- **API Documentation**: https://api.example.com/swagger
- **GitHub Issues**: https://github.com/scholtz/BiatecTokensApi/issues
- **Email Support**: Use the correlation ID from error responses

---

## Changelog

- **2026-02-03**: Initial integration guide
  - Added ARC-0014 authentication documentation
  - Added error handling with remediation hints
  - Added idempotency support documentation
  - Added subscription management examples
  - Added deployment tracking examples

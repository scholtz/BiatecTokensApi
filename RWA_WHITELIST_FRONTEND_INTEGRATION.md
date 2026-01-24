# RWA Whitelist Management - Frontend Integration Guide

## Overview

This guide provides comprehensive instructions for frontend developers to integrate with the RWA (Real World Asset) whitelist management API endpoints. The API enables enterprise token issuers on VOI/Aramid networks to manage token-holder whitelists for regulated deployments with MICA compliance.

## Table of Contents

1. [Authentication](#authentication)
2. [API Endpoints Overview](#api-endpoints-overview)
3. [Integration Patterns](#integration-patterns)
4. [Code Examples](#code-examples)
5. [Error Handling](#error-handling)
6. [Best Practices](#best-practices)

## Authentication

### ARC-0014 Authentication

All whitelist management endpoints require ARC-0014 authentication. The API expects a signed transaction in the `Authorization` header.

#### Authentication Flow

```javascript
// 1. Get user's Algorand address
const userAddress = await getUserAlgorandAddress();

// 2. Create authentication transaction
const authTx = await createARC14AuthTransaction({
  from: userAddress,
  realm: 'BiatecTokens#ARC14',
  expiresAt: Date.now() + 300000 // 5 minutes
});

// 3. Sign the transaction
const signedTx = await signTransaction(authTx);

// 4. Include in API requests
const headers = {
  'Authorization': `SigTx ${signedTx}`,
  'Content-Type': 'application/json'
};
```

#### Required Headers

```
Authorization: SigTx <base64-encoded-signed-transaction>
Content-Type: application/json
```

## API Endpoints Overview

### Base URL

```
https://api.biatectokens.com/api/v1/whitelist
```

### Available Endpoints

| Method | Endpoint | Description | Auth Required |
|--------|----------|-------------|---------------|
| GET | `/whitelist/{assetId}` | List whitelist entries | ✅ Yes |
| POST | `/whitelist` | Add single address | ✅ Yes |
| DELETE | `/whitelist` | Remove address | ✅ Yes |
| POST | `/whitelist/bulk` | Bulk add addresses | ✅ Yes |
| GET | `/whitelist/{assetId}/audit-log` | Get audit log | ✅ Yes |
| GET | `/whitelist/audit-log` | Get all audit logs | ✅ Yes |
| GET | `/whitelist/audit-log/export/csv` | Export audit log as CSV | ✅ Yes |
| GET | `/whitelist/audit-log/export/json` | Export audit log as JSON | ✅ Yes |

## Integration Patterns

### Pattern 1: List Whitelist Entries (Read-Only View)

**Use Case**: Display current whitelist for a token in admin dashboard.

#### Request

```http
GET /api/v1/whitelist/{assetId}?page=1&pageSize=20&status=Active
Authorization: SigTx <signed-transaction>
```

#### Query Parameters

- `assetId` (required): The token's asset ID
- `status` (optional): Filter by status (Active, Inactive, Revoked)
- `page` (optional): Page number (default: 1)
- `pageSize` (optional): Results per page (default: 20, max: 100)

#### Response

```json
{
  "success": true,
  "entries": [
    {
      "id": "550e8400-e29b-41d4-a716-446655440000",
      "assetId": 12345,
      "address": "ADDR1234567890ABCDEFGHIJKLMNOPQRSTUVWXYZ234567890ABCDEFGH",
      "status": "Active",
      "createdBy": "ADMIN123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ234567890ABCDEFGH",
      "createdAt": "2026-01-24T10:30:00Z",
      "updatedAt": null,
      "updatedBy": null,
      "reason": "KYC verified",
      "expirationDate": null,
      "kycVerified": true,
      "kycVerificationDate": "2026-01-24T09:00:00Z",
      "kycProvider": "VerifyMe Inc",
      "network": "voimain-v1.0",
      "role": "Admin"
    }
  ],
  "totalCount": 150,
  "page": 1,
  "pageSize": 20,
  "totalPages": 8
}
```

#### Frontend Implementation Example

```typescript
interface WhitelistEntry {
  id: string;
  assetId: number;
  address: string;
  status: 'Active' | 'Inactive' | 'Revoked';
  createdBy: string;
  createdAt: string;
  reason?: string;
  kycVerified: boolean;
  network?: string;
}

interface ListWhitelistResponse {
  success: boolean;
  entries: WhitelistEntry[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  errorMessage?: string;
}

async function listWhitelist(
  assetId: number,
  page: number = 1,
  status?: string
): Promise<ListWhitelistResponse> {
  const params = new URLSearchParams({
    page: page.toString(),
    pageSize: '20'
  });
  
  if (status) {
    params.append('status', status);
  }
  
  const response = await fetch(
    `https://api.biatectokens.com/api/v1/whitelist/${assetId}?${params}`,
    {
      method: 'GET',
      headers: await getAuthHeaders()
    }
  );
  
  if (!response.ok) {
    throw new Error(`Failed to list whitelist: ${response.statusText}`);
  }
  
  return await response.json();
}
```

### Pattern 2: Add Single Address to Whitelist

**Use Case**: Admin adds a new verified address to the whitelist.

#### Request

```http
POST /api/v1/whitelist
Authorization: SigTx <signed-transaction>
Content-Type: application/json

{
  "assetId": 12345,
  "address": "NEWADDR1234567890ABCDEFGHIJKLMNOPQRSTUVWXYZ234567890ABC",
  "status": "Active",
  "reason": "KYC verified - Accredited investor",
  "expirationDate": "2027-01-24T00:00:00Z",
  "kycVerified": true,
  "kycVerificationDate": "2026-01-24T09:00:00Z",
  "kycProvider": "VerifyMe Inc",
  "network": "voimain-v1.0",
  "role": "Admin"
}
```

#### Request Body Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| assetId | number | ✅ Yes | Token asset ID |
| address | string | ✅ Yes | Algorand address (58 characters) |
| status | string | No | Active (default), Inactive, Revoked |
| reason | string | No | Reason for whitelisting |
| expirationDate | string | No | ISO 8601 date when entry expires |
| kycVerified | boolean | No | KYC verification status |
| kycVerificationDate | string | No | ISO 8601 date of KYC completion |
| kycProvider | string | No | Name of KYC provider |
| network | string | No | Network (voimain-v1.0, aramidmain-v1.0) |
| role | string | No | Admin (default) or Operator |

#### Response

```json
{
  "success": true,
  "entry": {
    "id": "550e8400-e29b-41d4-a716-446655440001",
    "assetId": 12345,
    "address": "NEWADDR1234567890ABCDEFGHIJKLMNOPQRSTUVWXYZ234567890ABC",
    "status": "Active",
    "createdBy": "ADMIN123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ234567890ABCDEFGH",
    "createdAt": "2026-01-24T11:00:00Z",
    "reason": "KYC verified - Accredited investor",
    "kycVerified": true,
    "network": "voimain-v1.0"
  }
}
```

#### Frontend Implementation Example

```typescript
interface AddWhitelistRequest {
  assetId: number;
  address: string;
  status?: string;
  reason?: string;
  expirationDate?: string;
  kycVerified?: boolean;
  kycVerificationDate?: string;
  kycProvider?: string;
  network?: string;
  role?: string;
}

async function addWhitelistEntry(
  request: AddWhitelistRequest
): Promise<WhitelistResponse> {
  const response = await fetch(
    'https://api.biatectokens.com/api/v1/whitelist',
    {
      method: 'POST',
      headers: await getAuthHeaders(),
      body: JSON.stringify(request)
    }
  );
  
  const result = await response.json();
  
  if (!response.ok || !result.success) {
    throw new Error(result.errorMessage || 'Failed to add whitelist entry');
  }
  
  return result;
}

// Usage example
async function handleAddAddress() {
  try {
    const result = await addWhitelistEntry({
      assetId: 12345,
      address: userInputAddress,
      status: 'Active',
      reason: 'KYC verified',
      kycVerified: true,
      network: 'voimain-v1.0'
    });
    
    console.log('Address added successfully:', result.entry);
    // Update UI to show success
  } catch (error) {
    console.error('Failed to add address:', error);
    // Show error message to user
  }
}
```

### Pattern 3: Remove Address from Whitelist

**Use Case**: Admin removes an address from the whitelist (compliance violation, token sale completed, etc.).

#### Request

```http
DELETE /api/v1/whitelist
Authorization: SigTx <signed-transaction>
Content-Type: application/json

{
  "assetId": 12345,
  "address": "ADDR1234567890ABCDEFGHIJKLMNOPQRSTUVWXYZ234567890ABCDEFGH"
}
```

#### Response

```json
{
  "success": true,
  "message": "Whitelist entry removed successfully"
}
```

#### Frontend Implementation Example

```typescript
async function removeWhitelistEntry(
  assetId: number,
  address: string
): Promise<void> {
  const response = await fetch(
    'https://api.biatectokens.com/api/v1/whitelist',
    {
      method: 'DELETE',
      headers: await getAuthHeaders(),
      body: JSON.stringify({ assetId, address })
    }
  );
  
  const result = await response.json();
  
  if (!response.ok || !result.success) {
    throw new Error(result.errorMessage || 'Failed to remove whitelist entry');
  }
}

// Usage example with confirmation
async function handleRemoveAddress(assetId: number, address: string) {
  if (!confirm('Are you sure you want to remove this address from the whitelist?')) {
    return;
  }
  
  try {
    await removeWhitelistEntry(assetId, address);
    console.log('Address removed successfully');
    // Refresh the whitelist display
    await refreshWhitelistDisplay(assetId);
  } catch (error) {
    console.error('Failed to remove address:', error);
    alert('Error removing address. Please try again.');
  }
}
```

### Pattern 4: Bulk Upload Addresses

**Use Case**: Admin uploads multiple addresses at once (e.g., from CSV file after batch KYC verification).

#### Request

```http
POST /api/v1/whitelist/bulk
Authorization: SigTx <signed-transaction>
Content-Type: application/json

{
  "assetId": 12345,
  "entries": [
    {
      "address": "ADDR1_1234567890ABCDEFGHIJKLMNOPQRSTUVWXYZ234567890ABC",
      "status": "Active",
      "reason": "KYC verified",
      "kycVerified": true,
      "network": "voimain-v1.0"
    },
    {
      "address": "ADDR2_1234567890ABCDEFGHIJKLMNOPQRSTUVWXYZ234567890ABC",
      "status": "Active",
      "reason": "Accredited investor",
      "kycVerified": true,
      "network": "voimain-v1.0"
    }
  ]
}
```

#### Response

```json
{
  "success": true,
  "successCount": 2,
  "failedCount": 0,
  "results": [
    {
      "address": "ADDR1_1234567890ABCDEFGHIJKLMNOPQRSTUVWXYZ234567890ABC",
      "success": true,
      "message": "Entry added successfully"
    },
    {
      "address": "ADDR2_1234567890ABCDEFGHIJKLMNOPQRSTUVWXYZ234567890ABC",
      "success": true,
      "message": "Entry added successfully"
    }
  ]
}
```

#### Frontend Implementation Example

```typescript
interface BulkWhitelistEntry {
  address: string;
  status?: string;
  reason?: string;
  kycVerified?: boolean;
  network?: string;
}

interface BulkWhitelistRequest {
  assetId: number;
  entries: BulkWhitelistEntry[];
}

interface BulkWhitelistResponse {
  success: boolean;
  successCount: number;
  failedCount: number;
  results: Array<{
    address: string;
    success: boolean;
    message: string;
  }>;
}

async function bulkAddWhitelistEntries(
  request: BulkWhitelistRequest
): Promise<BulkWhitelistResponse> {
  const response = await fetch(
    'https://api.biatectokens.com/api/v1/whitelist/bulk',
    {
      method: 'POST',
      headers: await getAuthHeaders(),
      body: JSON.stringify(request)
    }
  );
  
  return await response.json();
}

// CSV Upload Example
async function handleCsvUpload(file: File, assetId: number) {
  const text = await file.text();
  const lines = text.split('\n').slice(1); // Skip header row
  
  const entries: BulkWhitelistEntry[] = lines
    .filter(line => line.trim())
    .map(line => {
      const [address, reason, kycVerified] = line.split(',');
      return {
        address: address.trim(),
        status: 'Active',
        reason: reason?.trim(),
        kycVerified: kycVerified?.trim().toLowerCase() === 'true',
        network: 'voimain-v1.0'
      };
    });
  
  try {
    const result = await bulkAddWhitelistEntries({
      assetId,
      entries
    });
    
    console.log(`Bulk upload complete: ${result.successCount} succeeded, ${result.failedCount} failed`);
    
    // Show results to user
    if (result.failedCount > 0) {
      const failedAddresses = result.results
        .filter(r => !r.success)
        .map(r => `${r.address}: ${r.message}`)
        .join('\n');
      
      alert(`Some addresses failed:\n${failedAddresses}`);
    } else {
      alert('All addresses added successfully!');
    }
    
    // Refresh display
    await refreshWhitelistDisplay(assetId);
  } catch (error) {
    console.error('Bulk upload failed:', error);
    alert('Error during bulk upload. Please try again.');
  }
}
```

### Pattern 5: Query Audit Log for Compliance Reporting

**Use Case**: Display audit trail for compliance officers or regulatory reporting.

#### Request

```http
GET /api/v1/whitelist/{assetId}/audit-log?page=1&pageSize=50&fromDate=2026-01-01T00:00:00Z
Authorization: SigTx <signed-transaction>
```

#### Query Parameters

- `assetId` (required): Token asset ID
- `address` (optional): Filter by specific address
- `actionType` (optional): Filter by action (Add, Update, Remove, TransferValidation)
- `performedBy` (optional): Filter by actor's address
- `fromDate` (optional): Start date (ISO 8601)
- `toDate` (optional): End date (ISO 8601)
- `page` (optional): Page number (default: 1)
- `pageSize` (optional): Results per page (default: 50, max: 100)

#### Response

```json
{
  "success": true,
  "entries": [
    {
      "id": "audit-550e8400-e29b-41d4-a716-446655440000",
      "assetId": 12345,
      "address": "ADDR1234567890ABCDEFGHIJKLMNOPQRSTUVWXYZ234567890ABCDEFGH",
      "actionType": "Add",
      "performedBy": "ADMIN123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ234567890ABCDEFGH",
      "performedAt": "2026-01-24T10:30:00Z",
      "oldStatus": null,
      "newStatus": "Active",
      "notes": "KYC verified",
      "network": "voimain-v1.0",
      "role": "Admin"
    },
    {
      "id": "audit-550e8400-e29b-41d4-a716-446655440001",
      "assetId": 12345,
      "address": "ADDR1234567890ABCDEFGHIJKLMNOPQRSTUVWXYZ234567890ABCDEFGH",
      "actionType": "Update",
      "performedBy": "ADMIN123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ234567890ABCDEFGH",
      "performedAt": "2026-01-24T15:30:00Z",
      "oldStatus": "Active",
      "newStatus": "Revoked",
      "notes": "Compliance violation detected",
      "network": "voimain-v1.0",
      "role": "Admin"
    }
  ],
  "totalCount": 245,
  "page": 1,
  "pageSize": 50,
  "totalPages": 5,
  "retentionPolicy": {
    "minimumRetentionYears": 7,
    "regulatoryFramework": "MICA",
    "immutableEntries": true,
    "description": "Audit logs are retained for a minimum of 7 years..."
  }
}
```

#### Frontend Implementation Example

```typescript
interface AuditLogEntry {
  id: string;
  assetId: number;
  address: string;
  actionType: 'Add' | 'Update' | 'Remove' | 'TransferValidation';
  performedBy: string;
  performedAt: string;
  oldStatus?: string;
  newStatus?: string;
  notes?: string;
  network?: string;
}

interface AuditLogResponse {
  success: boolean;
  entries: AuditLogEntry[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  retentionPolicy?: any;
}

async function getAuditLog(
  assetId: number,
  filters?: {
    address?: string;
    actionType?: string;
    performedBy?: string;
    fromDate?: string;
    toDate?: string;
    page?: number;
  }
): Promise<AuditLogResponse> {
  const params = new URLSearchParams({
    page: (filters?.page || 1).toString(),
    pageSize: '50'
  });
  
  if (filters?.address) params.append('address', filters.address);
  if (filters?.actionType) params.append('actionType', filters.actionType);
  if (filters?.performedBy) params.append('performedBy', filters.performedBy);
  if (filters?.fromDate) params.append('fromDate', filters.fromDate);
  if (filters?.toDate) params.append('toDate', filters.toDate);
  
  const response = await fetch(
    `https://api.biatectokens.com/api/v1/whitelist/${assetId}/audit-log?${params}`,
    {
      method: 'GET',
      headers: await getAuthHeaders()
    }
  );
  
  return await response.json();
}

// Usage example: Compliance dashboard
async function renderComplianceDashboard(assetId: number) {
  const last30Days = new Date();
  last30Days.setDate(last30Days.getDate() - 30);
  
  const auditLog = await getAuditLog(assetId, {
    fromDate: last30Days.toISOString(),
    page: 1
  });
  
  // Group by action type for dashboard
  const actionCounts = auditLog.entries.reduce((acc, entry) => {
    acc[entry.actionType] = (acc[entry.actionType] || 0) + 1;
    return acc;
  }, {} as Record<string, number>);
  
  console.log('Activity in last 30 days:', actionCounts);
  // Render dashboard UI with this data
}
```

### Pattern 6: Export Audit Log (CSV/JSON)

**Use Case**: Download audit trail for external compliance systems or regulatory submissions.

#### CSV Export Request

```http
GET /api/v1/whitelist/audit-log/export/csv?assetId=12345&fromDate=2026-01-01T00:00:00Z
Authorization: SigTx <signed-transaction>
```

#### Frontend Implementation Example

```typescript
async function exportAuditLogCsv(
  filters?: {
    assetId?: number;
    network?: string;
    fromDate?: string;
    toDate?: string;
  }
): Promise<void> {
  const params = new URLSearchParams();
  
  if (filters?.assetId) params.append('assetId', filters.assetId.toString());
  if (filters?.network) params.append('network', filters.network);
  if (filters?.fromDate) params.append('fromDate', filters.fromDate);
  if (filters?.toDate) params.append('toDate', filters.toDate);
  
  const response = await fetch(
    `https://api.biatectokens.com/api/v1/whitelist/audit-log/export/csv?${params}`,
    {
      method: 'GET',
      headers: await getAuthHeaders()
    }
  );
  
  if (!response.ok) {
    throw new Error('Failed to export audit log');
  }
  
  // Download file
  const blob = await response.blob();
  const url = window.URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = `audit-log-${Date.now()}.csv`;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  window.URL.revokeObjectURL(url);
}

// Usage example: Export button
async function handleExportClick(assetId: number) {
  try {
    await exportAuditLogCsv({
      assetId,
      fromDate: '2026-01-01T00:00:00Z'
    });
    console.log('Audit log exported successfully');
  } catch (error) {
    console.error('Export failed:', error);
    alert('Failed to export audit log');
  }
}
```

## Error Handling

### Common Error Responses

#### 400 Bad Request

```json
{
  "success": false,
  "errorMessage": "Invalid address format. Address must be 58 characters."
}
```

**Causes**:
- Invalid Algorand address format
- Missing required fields
- Invalid enum values

**Frontend Handling**:
```typescript
try {
  await addWhitelistEntry(request);
} catch (error) {
  if (error.status === 400) {
    // Show validation error to user
    showValidationError(error.message);
  }
}
```

#### 401 Unauthorized

```json
{
  "success": false,
  "errorMessage": "User address not found in authentication context"
}
```

**Causes**:
- Missing or invalid ARC-0014 authentication
- Expired authentication token
- Invalid signature

**Frontend Handling**:
```typescript
try {
  await listWhitelist(assetId);
} catch (error) {
  if (error.status === 401) {
    // Redirect to authentication
    redirectToLogin();
  }
}
```

#### 404 Not Found

```json
{
  "success": false,
  "errorMessage": "Whitelist entry not found"
}
```

**Causes**:
- Address not in whitelist
- Invalid asset ID

**Frontend Handling**:
```typescript
try {
  await removeWhitelistEntry(assetId, address);
} catch (error) {
  if (error.status === 404) {
    alert('Address is not in the whitelist');
  }
}
```

#### 500 Internal Server Error

```json
{
  "success": false,
  "errorMessage": "Internal error: Database connection failed"
}
```

**Frontend Handling**:
```typescript
try {
  await bulkAddWhitelistEntries(request);
} catch (error) {
  if (error.status === 500) {
    // Show generic error and log details
    console.error('Server error:', error);
    alert('An unexpected error occurred. Please try again later.');
  }
}
```

### Retry Logic

```typescript
async function retryRequest<T>(
  requestFn: () => Promise<T>,
  maxRetries: number = 3,
  delay: number = 1000
): Promise<T> {
  for (let i = 0; i < maxRetries; i++) {
    try {
      return await requestFn();
    } catch (error) {
      if (i === maxRetries - 1) throw error;
      
      // Only retry on network errors or 5xx
      if (error.status >= 500 || !error.status) {
        await new Promise(resolve => setTimeout(resolve, delay * (i + 1)));
      } else {
        throw error;
      }
    }
  }
  
  throw new Error('Max retries exceeded');
}

// Usage
const result = await retryRequest(() => addWhitelistEntry(request));
```

## Best Practices

### 1. Address Validation

Always validate Algorand addresses on the frontend before sending to API:

```typescript
function isValidAlgorandAddress(address: string): boolean {
  // Algorand addresses are 58 characters
  if (address.length !== 58) {
    return false;
  }
  
  // Contains only uppercase letters and numbers
  if (!/^[A-Z2-7]+$/.test(address)) {
    return false;
  }
  
  return true;
}

// Use before API calls
if (!isValidAlgorandAddress(userInput)) {
  showError('Invalid Algorand address format');
  return;
}
```

### 2. Pagination Handling

Implement proper pagination for large whitelists:

```typescript
class WhitelistPaginator {
  private currentPage = 1;
  private pageSize = 20;
  
  async loadPage(assetId: number, page: number) {
    const response = await listWhitelist(assetId, page);
    this.currentPage = response.page;
    return response;
  }
  
  async loadNext(assetId: number) {
    return this.loadPage(assetId, this.currentPage + 1);
  }
  
  async loadPrevious(assetId: number) {
    if (this.currentPage > 1) {
      return this.loadPage(assetId, this.currentPage - 1);
    }
    return null;
  }
}
```

### 3. Optimistic UI Updates

Update UI immediately while API request is in progress:

```typescript
async function optimisticAddWhitelist(entry: AddWhitelistRequest) {
  // Add to UI immediately
  const tempEntry = {
    ...entry,
    id: 'temp-' + Date.now(),
    createdAt: new Date().toISOString(),
    pending: true
  };
  
  addToLocalWhitelistCache(tempEntry);
  updateUI();
  
  try {
    // Make API call
    const result = await addWhitelistEntry(entry);
    
    // Replace temp entry with real one
    replaceInLocalWhitelistCache(tempEntry.id, result.entry);
    updateUI();
  } catch (error) {
    // Remove temp entry on error
    removeFromLocalWhitelistCache(tempEntry.id);
    updateUI();
    throw error;
  }
}
```

### 4. Caching Strategy

Cache whitelist data with proper invalidation:

```typescript
class WhitelistCache {
  private cache = new Map<string, { data: any; timestamp: number }>();
  private ttl = 60000; // 1 minute
  
  getCacheKey(assetId: number, page: number, status?: string): string {
    return `${assetId}-${page}-${status || 'all'}`;
  }
  
  get(key: string): any | null {
    const cached = this.cache.get(key);
    if (!cached) return null;
    
    if (Date.now() - cached.timestamp > this.ttl) {
      this.cache.delete(key);
      return null;
    }
    
    return cached.data;
  }
  
  set(key: string, data: any): void {
    this.cache.set(key, {
      data,
      timestamp: Date.now()
    });
  }
  
  invalidate(assetId: number): void {
    // Remove all cached entries for this asset
    for (const key of this.cache.keys()) {
      if (key.startsWith(`${assetId}-`)) {
        this.cache.delete(key);
      }
    }
  }
}

// Usage
const cache = new WhitelistCache();

async function getCachedWhitelist(assetId: number, page: number) {
  const key = cache.getCacheKey(assetId, page);
  const cached = cache.get(key);
  
  if (cached) {
    return cached;
  }
  
  const fresh = await listWhitelist(assetId, page);
  cache.set(key, fresh);
  return fresh;
}

// Invalidate after mutations
async function addAndInvalidate(request: AddWhitelistRequest) {
  await addWhitelistEntry(request);
  cache.invalidate(request.assetId);
}
```

### 5. Network-Specific Handling

Handle VOI and Aramid network differences:

```typescript
const NETWORK_REQUIREMENTS = {
  'voimain-v1.0': {
    kycRequired: false, // Recommended but not mandatory
    displayName: 'VOI Mainnet'
  },
  'aramidmain-v1.0': {
    kycRequired: true, // Mandatory for Active status
    displayName: 'Aramid Mainnet'
  }
};

function validateNetworkRequirements(
  network: string,
  entry: AddWhitelistRequest
): boolean {
  const requirements = NETWORK_REQUIREMENTS[network];
  
  if (!requirements) {
    throw new Error(`Unknown network: ${network}`);
  }
  
  if (requirements.kycRequired && 
      entry.status === 'Active' && 
      !entry.kycVerified) {
    throw new Error(
      `${requirements.displayName} requires KYC verification for Active status`
    );
  }
  
  return true;
}
```

### 6. Audit Log Monitoring

Implement real-time monitoring for compliance officers:

```typescript
class AuditLogMonitor {
  private intervalId?: number;
  private lastChecked: Date;
  
  constructor(private assetId: number) {
    this.lastChecked = new Date();
  }
  
  start(callback: (newEntries: AuditLogEntry[]) => void): void {
    this.intervalId = window.setInterval(async () => {
      const result = await getAuditLog(this.assetId, {
        fromDate: this.lastChecked.toISOString()
      });
      
      if (result.entries.length > 0) {
        callback(result.entries);
        this.lastChecked = new Date();
      }
    }, 30000); // Check every 30 seconds
  }
  
  stop(): void {
    if (this.intervalId) {
      clearInterval(this.intervalId);
    }
  }
}

// Usage
const monitor = new AuditLogMonitor(12345);
monitor.start((newEntries) => {
  console.log('New audit entries:', newEntries);
  showNotification(`${newEntries.length} new audit entries`);
});
```

## Complete Integration Example

Here's a complete React component example integrating all patterns:

```typescript
import React, { useState, useEffect } from 'react';

interface WhitelistManagerProps {
  assetId: number;
  network: string;
}

const WhitelistManager: React.FC<WhitelistManagerProps> = ({ assetId, network }) => {
  const [entries, setEntries] = useState<WhitelistEntry[]>([]);
  const [loading, setLoading] = useState(false);
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [newAddress, setNewAddress] = useState('');
  
  // Load whitelist entries
  useEffect(() => {
    loadEntries();
  }, [assetId, page]);
  
  async function loadEntries() {
    setLoading(true);
    try {
      const result = await listWhitelist(assetId, page);
      setEntries(result.entries);
      setTotalPages(result.totalPages);
    } catch (error) {
      console.error('Failed to load whitelist:', error);
      alert('Error loading whitelist');
    } finally {
      setLoading(false);
    }
  }
  
  async function handleAddAddress() {
    if (!isValidAlgorandAddress(newAddress)) {
      alert('Invalid Algorand address');
      return;
    }
    
    try {
      await addWhitelistEntry({
        assetId,
        address: newAddress,
        status: 'Active',
        kycVerified: true,
        network
      });
      
      setNewAddress('');
      alert('Address added successfully');
      await loadEntries();
    } catch (error) {
      console.error('Failed to add address:', error);
      alert('Error adding address');
    }
  }
  
  async function handleRemoveAddress(address: string) {
    if (!confirm(`Remove ${address} from whitelist?`)) {
      return;
    }
    
    try {
      await removeWhitelistEntry(assetId, address);
      alert('Address removed successfully');
      await loadEntries();
    } catch (error) {
      console.error('Failed to remove address:', error);
      alert('Error removing address');
    }
  }
  
  return (
    <div className="whitelist-manager">
      <h2>Whitelist Management - Asset {assetId}</h2>
      
      {/* Add Address Form */}
      <div className="add-address-form">
        <input
          type="text"
          value={newAddress}
          onChange={(e) => setNewAddress(e.target.value)}
          placeholder="Enter Algorand address"
          maxLength={58}
        />
        <button onClick={handleAddAddress}>Add Address</button>
      </div>
      
      {/* Whitelist Table */}
      {loading ? (
        <div>Loading...</div>
      ) : (
        <table className="whitelist-table">
          <thead>
            <tr>
              <th>Address</th>
              <th>Status</th>
              <th>KYC</th>
              <th>Created</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {entries.map(entry => (
              <tr key={entry.id}>
                <td>{entry.address}</td>
                <td>{entry.status}</td>
                <td>{entry.kycVerified ? '✓' : '✗'}</td>
                <td>{new Date(entry.createdAt).toLocaleDateString()}</td>
                <td>
                  <button onClick={() => handleRemoveAddress(entry.address)}>
                    Remove
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
      
      {/* Pagination */}
      <div className="pagination">
        <button 
          onClick={() => setPage(p => Math.max(1, p - 1))}
          disabled={page === 1}
        >
          Previous
        </button>
        <span>Page {page} of {totalPages}</span>
        <button 
          onClick={() => setPage(p => Math.min(totalPages, p + 1))}
          disabled={page === totalPages}
        >
          Next
        </button>
      </div>
    </div>
  );
};

export default WhitelistManager;
```

## Testing Your Integration

### 1. Test Authentication

```typescript
// Test with valid authentication
const result = await listWhitelist(12345);
console.assert(result.success === true, 'Should succeed with valid auth');

// Test without authentication (should fail)
try {
  await fetch('https://api.biatectokens.com/api/v1/whitelist/12345');
  console.error('Should have required authentication');
} catch (error) {
  console.assert(error.status === 401, 'Should return 401 Unauthorized');
}
```

### 2. Test Address Validation

```typescript
// Valid address
const valid = 'ADDR1234567890ABCDEFGHIJKLMNOPQRSTUVWXYZ234567890ABCDEFGH';
console.assert(valid.length === 58, 'Valid address is 58 characters');

// Invalid addresses
const invalid1 = 'TOO_SHORT';
const invalid2 = 'addr1234567890abcdefghijklmnopqrstuvwxyz234567890abcdefgh'; // lowercase
const invalid3 = 'ADDR1234567890ABCDEFGHIJKLMNOPQRSTUVWXYZ234567890ABCDEFG'; // 57 chars

console.assert(!isValidAlgorandAddress(invalid1), 'Should reject short address');
console.assert(!isValidAlgorandAddress(invalid2), 'Should reject lowercase');
console.assert(!isValidAlgorandAddress(invalid3), 'Should reject wrong length');
```

### 3. Test Pagination

```typescript
// Load first page
const page1 = await listWhitelist(12345, 1);
console.assert(page1.page === 1, 'Should be page 1');

// Load second page
const page2 = await listWhitelist(12345, 2);
console.assert(page2.page === 2, 'Should be page 2');
console.assert(page2.entries[0].id !== page1.entries[0].id, 'Pages should differ');
```

## Support and Resources

### Documentation Links

- [Swagger/OpenAPI Documentation](https://api.biatectokens.com/swagger)
- [ARC-0014 Authentication Standard](https://github.com/algorandfoundation/ARCs/blob/main/ARCs/arc-0014.md)
- [MICA Regulation Overview](https://eur-lex.europa.eu/legal-content/EN/TXT/?uri=CELEX:32023R1114)

### Common Questions

**Q: How long do audit logs stay in the system?**  
A: Audit logs are retained for a minimum of 7 years to comply with MICA regulations. All entries are immutable.

**Q: Can I update an existing whitelist entry?**  
A: Yes, but it's implemented as a remove + add operation. This creates a clear audit trail.

**Q: What's the difference between VOI and Aramid network requirements?**  
A: Aramid requires mandatory KYC verification for Active status. VOI recommends it but doesn't require it.

**Q: How many addresses can I add in a bulk operation?**  
A: There's no hard limit, but we recommend batches of 100-500 addresses for optimal performance.

**Q: Can operators remove addresses from the whitelist?**  
A: It depends on your access control configuration. By default, only Admins can remove addresses.

### Contact

For technical support or questions:
- GitHub Issues: https://github.com/scholtz/BiatecTokensApi/issues
- Email: support@biatectokens.com

## Changelog

### Version 1.0 (2026-01-24)
- Initial release
- CRUD endpoints for whitelist management
- Audit log query and export functionality
- ARC-0014 authentication integration
- Network-specific validation (VOI/Aramid)

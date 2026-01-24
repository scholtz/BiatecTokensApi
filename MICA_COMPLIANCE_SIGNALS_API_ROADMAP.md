# MICA Compliance Signals API Roadmap

**Document Version:** 1.0  
**Last Updated:** January 23, 2026  
**Purpose:** Define compliance signals API coverage for MICA dashboards

---

## Executive Summary

This roadmap defines the compliance signals API architecture for Markets in Crypto-Assets (MICA) dashboard requirements. It provides a structured approach to surface issuer verification, whitelist/blacklist status, and audit trail metadata to frontend applications, enabling real-time compliance monitoring and regulatory reporting.

### Key Objectives

1. **Issuer Verification** - Provide verifiable proof of issuer identity and credentials
2. **Whitelist/Blacklist Status** - Real-time access control and transfer restrictions
3. **Audit Trail Metadata** - Comprehensive compliance event tracking for regulatory reporting
4. **Readiness Indicators** - Frontend-friendly signals for compliance status visualization
5. **MICA Alignment** - Full compliance with EU MiCA regulatory framework

---

## Table of Contents

1. [Current State Assessment](#current-state-assessment)
2. [Compliance Signals Architecture](#compliance-signals-architecture)
3. [API Coverage Roadmap](#api-coverage-roadmap)
4. [Data Contracts](#data-contracts)
5. [Recommended Endpoints](#recommended-endpoints)
6. [Data Sources and Requirements](#data-sources-and-requirements)
7. [Frontend Integration Guide](#frontend-integration-guide)
8. [Implementation Phases](#implementation-phases)
9. [Security and Privacy Considerations](#security-and-privacy-considerations)
10. [Testing and Validation](#testing-and-validation)

---

## Current State Assessment

### ✅ Already Implemented

The BiatecTokensApi already has a robust compliance infrastructure:

| Feature | Status | Endpoint(s) | Documentation |
|---------|--------|-------------|---------------|
| **Compliance Metadata** | ✅ Complete | `GET/POST/DELETE /api/v1/compliance/{assetId}` | COMPLIANCE_API.md |
| **Compliance Indicators** | ✅ Complete | `GET /api/v1/token/{assetId}/compliance-indicators` | COMPLIANCE_INDICATORS_API.md |
| **Whitelist Management** | ✅ Complete | `POST/DELETE /api/v1/whitelist` | WHITELIST_FEATURE.md |
| **Whitelist Audit Log** | ✅ Complete | `GET /api/v1/whitelist/audit-log` | WHITELIST_AUDIT_ENDPOINTS_IMPLEMENTATION.md |
| **Compliance Audit Log** | ✅ Complete | `GET /api/v1/compliance/audit-log` | AUDIT_LOG_IMPLEMENTATION.md |
| **Compliance Attestations** | ✅ Complete | `GET/POST /api/v1/compliance/attestations` | ATTESTATIONS_API_VERIFICATION.md |
| **Attestation Packages** | ✅ Complete | `POST /api/v1/compliance/attestation-package` | ATTESTATION_PACKAGE_IMPLEMENTATION.md |
| **Compliance Reports** | ✅ Complete | `GET /api/v1/compliance/report` | VOI_ARAMID_COMPLIANCE_REPORT_API.md |
| **CSV/JSON Exports** | ✅ Complete | `GET /api/v1/compliance/*/export/{csv,json}` | Multiple docs |

### 🔨 Gaps Identified

| Gap | Priority | Impact | Recommended Solution |
|-----|----------|--------|---------------------|
| **Issuer Profile Management** | High | Can't verify issuer identity | New `/api/v1/issuer/profile` endpoints |
| **Issuer Verification Status** | High | No issuer KYB tracking | Extend issuer profile with verification fields |
| **Real-time Blacklist API** | Medium | Manual blacklist checks | New `/api/v1/compliance/blacklist` endpoints |
| **Compliance Health Monitoring** | Medium | No aggregate health view | New `/api/v1/compliance/health` endpoint |
| **Transfer Restrictions Query** | Medium | Must parse metadata | New `/api/v1/compliance/{assetId}/restrictions` endpoint |
| **MICA Compliance Checklist** | Low | Manual compliance validation | New `/api/v1/compliance/{assetId}/mica-checklist` endpoint |

---

## Compliance Signals Architecture

### Signal Categories

The compliance signals system is organized into four main categories:

1. **Issuer Signals** - Issuer verification, profile, credentials
2. **Token Signals** - Compliance status, regulatory framework, transfer restrictions
3. **Audit Signals** - Audit trail, change history, access logs
4. **Access Control Signals** - Whitelist status, blacklist status, role assignments

### Data Flow Architecture

```
┌─────────────────┐
│   Frontend      │
│   Dashboard     │
└────────┬────────┘
         │ REST API
         ▼
┌─────────────────────────────────┐
│  Compliance Signals API Layer   │
│  ┌─────────────────────────────┐│
│  │ GET /compliance/indicators  ││  ← Readiness Signals
│  │ GET /issuer/profile         ││  ← Issuer Verification
│  │ GET /whitelist/{address}    ││  ← Access Control
│  │ GET /compliance/audit-log   ││  ← Audit Trail
│  │ GET /compliance/report      ││  ← Comprehensive Report
│  └─────────────────────────────┘│
└────────┬────────────────────────┘
         │
         ▼
┌──────────────────────────────────┐
│   Data Sources                   │
│  ┌──────────────────────────────┐│
│  │ Compliance Metadata Store    ││
│  │ Whitelist/Blacklist Store    ││
│  │ Attestation Store            ││
│  │ Audit Log Store              ││
│  │ Issuer Profile Store         ││
│  └──────────────────────────────┘│
└──────────────────────────────────┘
```

---

## API Coverage Roadmap

### Phase 1: Core Signals (Q1 2026) - ✅ COMPLETE

**Status:** All core compliance signals are implemented and production-ready.

| Signal Type | Endpoint | Status | Notes |
|------------|----------|--------|-------|
| Compliance Indicators | `GET /api/v1/token/{assetId}/compliance-indicators` | ✅ Live | Enterprise readiness score, MICA status |
| Whitelist Status | `GET /api/v1/whitelist` | ✅ Live | List all whitelist entries |
| Compliance Metadata | `GET /api/v1/compliance/{assetId}` | ✅ Live | Full regulatory metadata |
| Audit Trail | `GET /api/v1/compliance/audit-log` | ✅ Live | Immutable audit logs |
| Attestations | `GET /api/v1/compliance/attestations` | ✅ Live | KYC/AML attestations |

### Phase 2: Enhanced Issuer Signals (Q2 2026) - 🔨 RECOMMENDED

**Status:** Gaps identified, implementation recommended.

| Signal Type | Endpoint | Status | Priority |
|------------|----------|--------|----------|
| Issuer Profile | `GET /api/v1/issuer/profile/{address}` | 🔨 To Build | **HIGH** |
| Issuer Verification | `GET /api/v1/issuer/verification/{address}` | 🔨 To Build | **HIGH** |
| Issuer Credentials | `GET /api/v1/issuer/credentials/{address}` | 🔨 To Build | Medium |
| Issuer Assets | `GET /api/v1/issuer/{address}/assets` | 🔨 To Build | Medium |

### Phase 3: Advanced Access Control (Q3 2026) - 🔨 RECOMMENDED

**Status:** Partially implemented, enhancements recommended.

| Signal Type | Endpoint | Status | Priority |
|------------|----------|--------|----------|
| Address Whitelist Check | `GET /api/v1/whitelist/{assetId}/check/{address}` | 🔨 To Build | **HIGH** |
| Blacklist Management | `POST/DELETE /api/v1/compliance/blacklist` | 🔨 To Build | **HIGH** |
| Blacklist Check | `GET /api/v1/compliance/blacklist/check` | 🔨 To Build | **HIGH** |
| Transfer Validation | `POST /api/v1/compliance/validate-transfer` | 🔨 To Build | Medium |
| Role-Based Access | `GET /api/v1/compliance/roles` | 🔨 To Build | Medium |

### Phase 4: Regulatory Reporting (Q4 2026) - ⚠️ PARTIAL

**Status:** Compliance reports exist, but can be enhanced.

| Signal Type | Endpoint | Status | Priority |
|------------|----------|--------|----------|
| MICA Compliance Checklist | `GET /api/v1/compliance/{assetId}/mica-checklist` | 🔨 To Build | **HIGH** |
| Regulatory Alerts | `GET /api/v1/compliance/alerts` | 🔨 To Build | Medium |
| Compliance Health Dashboard | `GET /api/v1/compliance/health` | 🔨 To Build | Medium |
| Transfer Restrictions Query | `GET /api/v1/compliance/{assetId}/restrictions` | 🔨 To Build | Low |
| Jurisdiction Coverage | `GET /api/v1/compliance/jurisdictions` | 🔨 To Build | Low |

---

## Data Contracts

This section defines the recommended data models for new compliance signal endpoints.


### 1. Issuer Profile (NEW - Recommended)

Represents an issuer profile for MICA compliance tracking.

**Key Fields:**
- `IssuerAddress` - Unique identifier (issuer's Algorand address)
- `LegalName` - Legal entity name
- `CountryOfIncorporation` - ISO country code
- `TaxIdentificationNumber` - Tax ID
- `KybStatus` - KYB verification status (Pending, Verified, Failed, Expired)
- `KybProvider` - Name of KYB provider
- `MicaLicenseStatus` - MICA license status (None, Applied, Approved, Denied, etc.)
- `MicaLicenseNumber` - MICA license number
- `RegisteredAddress` - Business registration address
- `PrimaryContact` - Primary contact information
- `ComplianceContact` - Compliance officer contact
- `Website` - Company website URL

**Response Example:**
```json
{
  "success": true,
  "profile": {
    "issuerAddress": "ISSUER1...",
    "legalName": "Acme Tokenization Corp",
    "doingBusinessAs": "Acme Tokens",
    "entityType": "Corporation",
    "countryOfIncorporation": "US",
    "taxIdentificationNumber": "12-3456789",
    "registrationNumber": "DE123456",
    "registeredAddress": {
      "addressLine1": "123 Main Street",
      "city": "New York",
      "stateOrProvince": "NY",
      "postalCode": "10001",
      "countryCode": "US"
    },
    "primaryContact": {
      "name": "John Doe",
      "email": "john.doe@acme.com",
      "phoneNumber": "+1-212-555-0100",
      "title": "CEO"
    },
    "kybStatus": "Verified",
    "kybProvider": "Sumsub",
    "kybVerifiedDate": "2026-01-15T00:00:00Z",
    "micaLicenseStatus": "Approved",
    "micaLicenseNumber": "MICA-EU-12345",
    "micaCompetentAuthority": "BaFin (Germany)",
    "createdAt": "2026-01-01T00:00:00Z",
    "updatedAt": "2026-01-15T00:00:00Z",
    "status": "Verified"
  }
}
```

### 2. Blacklist Entry (NEW - Recommended)

Represents a blacklisted address for compliance enforcement.

**Key Fields:**
- `Address` - Blacklisted address
- `AssetId` - Asset ID (token ID), or null for global blacklist
- `Reason` - Reason for blacklisting
- `Category` - Blacklist category (Sanctions, Fraud, MoneyLaundering, etc.)
- `Network` - Network where blacklist applies
- `Jurisdiction` - Jurisdiction that issued blacklist
- `Source` - Source of blacklist (OFAC, FinCEN, Chainalysis, etc.)
- `EffectiveDate` - Date blacklist entry becomes effective
- `ExpirationDate` - Date blacklist entry expires (null for permanent)
- `Status` - Blacklist status (Active, Inactive, UnderReview, Removed)

**Response Example:**
```json
{
  "success": true,
  "isBlacklisted": true,
  "entries": [
    {
      "id": "bl-001",
      "address": "SUSPICIOUS...",
      "assetId": null,
      "reason": "Listed on OFAC SDN list",
      "category": "Sanctions",
      "network": null,
      "jurisdiction": "US",
      "source": "OFAC",
      "referenceId": "SDN-12345",
      "effectiveDate": "2025-06-01T00:00:00Z",
      "expirationDate": null,
      "status": "Active",
      "createdBy": "COMPLIANCE_ADMIN",
      "createdAt": "2025-06-01T00:00:00Z"
    }
  ]
}
```

### 3. MICA Compliance Checklist (NEW - Recommended)

MICA compliance checklist for a specific token with requirement tracking.

**Key Fields:**
- `AssetId` - Asset ID (token ID)
- `OverallStatus` - Overall MICA compliance status (NotStarted, InProgress, FullyCompliant, etc.)
- `CompliancePercentage` - Compliance percentage (0-100)
- `Requirements` - List of MICA requirements with status
- `NextAction` - Next required action
- `EstimatedCompletionDate` - Estimated completion date

**Response Example:**
```json
{
  "success": true,
  "checklist": {
    "assetId": 12345,
    "overallStatus": "NearlyCompliant",
    "compliancePercentage": 85,
    "requirements": [
      {
        "id": "MICA-ART35",
        "category": "Issuer Identification",
        "description": "Issuer must be identified and verified",
        "isMet": true,
        "priority": "Critical",
        "evidence": "KYB verified by Sumsub on 2026-01-15",
        "metDate": "2026-01-15T00:00:00Z"
      },
      {
        "id": "MICA-ART36",
        "category": "White Paper",
        "description": "White paper must be published",
        "isMet": true,
        "priority": "Critical",
        "evidence": "White paper available at https://acme.com/whitepaper.pdf",
        "metDate": "2026-01-10T00:00:00Z"
      },
      {
        "id": "MICA-ART59",
        "category": "AML/CTF",
        "description": "AML/CTF procedures must be implemented",
        "isMet": false,
        "priority": "Critical",
        "recommendations": "Implement KYC attestation workflow for all holders"
      }
    ],
    "generatedAt": "2026-01-23T10:00:00Z",
    "nextAction": "Implement AML/CTF procedures (MICA-ART59)",
    "estimatedCompletionDate": "2026-02-15T00:00:00Z"
  }
}
```

### 4. Enhanced Compliance Indicators

The existing `TokenComplianceIndicators` model should be enhanced with issuer and blacklist information:

**Additional Fields Recommended:**
- `IssuerVerificationStatus` - Issuer verification status (Unverified, Pending, FullyVerified, etc.)
- `MicaLicenseStatus` - MICA license status (None, Applied, Approved, etc.)
- `BlacklistedAddressCount` - Number of blacklisted addresses for this token
- `IssuerKybVerified` - Whether issuer has KYB verification
- `IssuerLegalName` - Issuer legal name

---

## Recommended Endpoints

### Phase 2: Issuer Management Endpoints

#### 1. Get Issuer Profile
```
GET /api/v1/issuer/profile/{issuerAddress}
```
**Purpose:** Retrieve issuer profile information  
**Authentication:** ARC-0014 required  
**Response:** `IssuerProfileResponse`  
**Use Case:** Display issuer information on token details page

#### 2. Create/Update Issuer Profile
```
POST /api/v1/issuer/profile
```
**Purpose:** Create or update issuer profile  
**Authentication:** ARC-0014 required (issuer must own the address)  
**Request Body:** `UpsertIssuerProfileRequest`  
**Response:** `IssuerProfileResponse`  
**Use Case:** Onboard new issuers, update issuer information

#### 3. Get Issuer Verification Status
```
GET /api/v1/issuer/verification/{issuerAddress}
```
**Purpose:** Get detailed issuer verification status  
**Authentication:** ARC-0014 required  
**Response:** `IssuerVerificationResponse`  
**Use Case:** Verify issuer credentials before token purchase

#### 4. List Issuer Assets
```
GET /api/v1/issuer/{issuerAddress}/assets
```
**Purpose:** List all tokens issued by an issuer  
**Authentication:** ARC-0014 required  
**Response:** `IssuerAssetsResponse`  
**Use Case:** Display issuer portfolio

### Phase 3: Access Control Endpoints

#### 5. Check Whitelist Status
```
GET /api/v1/whitelist/{assetId}/check/{address}
```
**Purpose:** Check if an address is whitelisted for a specific token  
**Authentication:** ARC-0014 required  
**Response:** `WhitelistCheckResponse`  
**Use Case:** Pre-transfer validation

**Response Example:**
```json
{
  "success": true,
  "assetId": 12345,
  "address": "VCMJKWOY...",
  "isWhitelisted": true,
  "status": "Active",
  "role": "User",
  "kycVerified": true,
  "addedAt": "2026-01-15T00:00:00Z",
  "expiresAt": null,
  "canTransfer": true,
  "restrictions": []
}
```

#### 6. Add Blacklist Entry
```
POST /api/v1/compliance/blacklist
```
**Purpose:** Add an address to the blacklist  
**Authentication:** ARC-0014 required (admin/compliance role recommended)  
**Request Body:** `AddBlacklistEntryRequest`  
**Response:** `BlacklistResponse`  
**Use Case:** Compliance enforcement

#### 7. Check Blacklist Status
```
GET /api/v1/compliance/blacklist/check
```
**Purpose:** Check if an address is blacklisted  
**Authentication:** ARC-0014 required  
**Query Parameters:** `address`, `assetId` (optional), `network` (optional)  
**Response:** `BlacklistCheckResponse`  
**Use Case:** Pre-transfer screening

#### 8. Validate Transfer
```
POST /api/v1/compliance/validate-transfer
```
**Purpose:** Validate a proposed transfer against all compliance rules  
**Authentication:** ARC-0014 required  
**Request Body:** `ValidateTransferRequest`  
**Response:** `TransferValidationResponse`  
**Use Case:** Pre-transaction validation

**Request Example:**
```json
{
  "assetId": 12345,
  "fromAddress": "SENDER...",
  "toAddress": "RECEIVER...",
  "amount": 1000000,
  "network": "voimain-v1.0"
}
```

**Response Example:**
```json
{
  "success": true,
  "isValid": true,
  "canTransfer": true,
  "validations": [
    {
      "rule": "SenderWhitelisted",
      "passed": true,
      "message": "Sender is whitelisted"
    },
    {
      "rule": "ReceiverWhitelisted",
      "passed": true,
      "message": "Receiver is whitelisted"
    },
    {
      "rule": "NoBlacklist",
      "passed": true,
      "message": "No blacklist violations"
    }
  ],
  "violations": [],
  "warnings": []
}
```

### Phase 4: Regulatory Reporting Endpoints

#### 9. MICA Compliance Checklist
```
GET /api/v1/compliance/{assetId}/mica-checklist
```
**Purpose:** Get MICA compliance checklist for a token  
**Authentication:** ARC-0014 required  
**Response:** `MicaComplianceChecklistResponse`  
**Use Case:** Dashboard compliance status widget

#### 10. Compliance Health Dashboard
```
GET /api/v1/compliance/health
```
**Purpose:** Get aggregate compliance health across all issuer tokens  
**Authentication:** ARC-0014 required  
**Query Parameters:** `issuerAddress`, `network`  
**Response:** `ComplianceHealthResponse`  
**Use Case:** Executive dashboard

**Response Example:**
```json
{
  "success": true,
  "overallHealthScore": 87,
  "totalTokens": 15,
  "compliantTokens": 12,
  "underReviewTokens": 2,
  "nonCompliantTokens": 1,
  "micaReadyTokens": 10,
  "tokensWithWhitelisting": 14,
  "tokensWithAuditTrail": 15,
  "issuerVerified": true,
  "alerts": [
    {
      "severity": "Warning",
      "message": "2 tokens have compliance review due within 30 days",
      "affectedAssetIds": [12345, 67890]
    }
  ],
  "recommendations": [
    "Complete MICA compliance checklist for asset 11111",
    "Renew KYC verification for 3 whitelisted addresses"
  ]
}
```

---

## Data Sources and Requirements

### Data Source Mapping

| Signal Type | Primary Source | Secondary Source | Refresh Rate |
|------------|----------------|------------------|--------------|
| **Issuer Verification** | Issuer Profile Store | KYB Provider API | On-demand |
| **Compliance Metadata** | Compliance Metadata Store | - | Real-time |
| **Whitelist Status** | Whitelist Store | Smart Contract | Real-time |
| **Blacklist Status** | Blacklist Store | External Screening APIs | Real-time |
| **Audit Trail** | Audit Log Store | - | Real-time (append-only) |
| **Attestations** | Attestation Store | IPFS (proof hash) | Real-time |
| **Compliance Reports** | Aggregated from multiple stores | - | On-demand |
| **Health Metrics** | Calculated from compliance data | - | Cached (5 min TTL) |

### External Data Requirements

#### KYB Providers
- **Sumsub** - Business verification
- **ComplyAdvantage** - AML/sanctions screening
- **Chainalysis** - On-chain risk assessment

**Integration Points:**
- Webhook for status updates
- API for verification queries
- Document upload endpoints

#### Sanctions Lists
- **OFAC** (Office of Foreign Assets Control) - US sanctions
- **UN Security Council** - Global sanctions
- **EU Sanctions** - European Union sanctions
- **Chainalysis Sanctions Oracle** - On-chain screening

**Requirements:**
- Daily sync of blacklists
- Real-time screening API
- Historical data retention

#### Regulatory Databases
- **ESMA Register** - EU regulatory register
- **SEC EDGAR** - US securities filings
- **MiCA Register** - EU crypto asset service providers

**Requirements:**
- Periodic validation of licenses
- API access for automated checks

### Storage Requirements

| Data Type | Storage | Retention | Backup |
|-----------|---------|-----------|--------|
| Issuer Profiles | Database | Indefinite | Daily |
| Compliance Metadata | Database | Indefinite | Daily |
| Whitelist Entries | Database | Indefinite | Daily |
| Blacklist Entries | Database | 7+ years | Daily |
| Audit Logs | Database | 7+ years (MICA requirement) | Daily |
| Attestations | Database + IPFS | Indefinite | Daily |
| Compliance Reports | Generated on-demand | 7+ years (exports) | Weekly |

---

## Frontend Integration Guide

### Dashboard Components

#### 1. Compliance Status Widget

React/TypeScript example for displaying compliance status:

```typescript
import { useComplianceIndicators } from '@/hooks/useCompliance';

export const ComplianceStatusWidget = ({ assetId }: { assetId: number }) => {
  const { data: indicators, isLoading } = useComplianceIndicators(assetId);
  
  if (isLoading) return <Skeleton />;
  
  return (
    <Card>
      <CardHeader>
        <CardTitle>Compliance Status</CardTitle>
      </CardHeader>
      <CardContent>
        <div className="space-y-4">
          <Badge variant={indicators.isMicaReady ? 'success' : 'warning'}>
            {indicators.isMicaReady ? '✓ MICA Ready' : '⚠ MICA Incomplete'}
          </Badge>
          
          <div>
            <Label>Enterprise Readiness</Label>
            <Progress value={indicators.enterpriseReadinessScore} />
            <Text>{indicators.enterpriseReadinessScore}/100</Text>
          </div>
          
          <div>
            <Label>Compliance Status</Label>
            <StatusBadge status={indicators.complianceStatus} />
          </div>
        </div>
      </CardContent>
    </Card>
  );
};
```

#### 2. MICA Compliance Checklist Widget

```typescript
export const MicaChecklistWidget = ({ assetId }: { assetId: number }) => {
  const { data: checklist } = useMicaChecklist(assetId);
  
  return (
    <Card>
      <CardHeader>
        <CardTitle>MICA Compliance Checklist</CardTitle>
        <Badge>{checklist.compliancePercentage}% Complete</Badge>
      </CardHeader>
      <CardContent>
        <div className="space-y-2">
          {checklist.requirements.map(req => (
            <div key={req.id} className="flex items-center gap-2">
              <Checkbox checked={req.isMet} disabled />
              <Label className={req.isMet ? 'line-through' : ''}>
                {req.description}
              </Label>
              {req.priority === 'Critical' && !req.isMet && (
                <Badge variant="destructive">Critical</Badge>
              )}
            </div>
          ))}
        </div>
      </CardContent>
    </Card>
  );
};
```

### API Client Hooks

```typescript
// hooks/useCompliance.ts
import { useQuery, useMutation } from '@tanstack/react-query';
import { apiClient } from '@/lib/api';

export const useComplianceIndicators = (assetId: number) => {
  return useQuery({
    queryKey: ['compliance-indicators', assetId],
    queryFn: () => apiClient.getComplianceIndicators(assetId),
    staleTime: 5 * 60 * 1000, // 5 minutes
  });
};

export const useMicaChecklist = (assetId: number) => {
  return useQuery({
    queryKey: ['mica-checklist', assetId],
    queryFn: () => apiClient.getMicaChecklist(assetId),
    staleTime: 10 * 60 * 1000, // 10 minutes
  });
};

export const useValidateTransfer = () => {
  return useMutation({
    mutationFn: (request: ValidateTransferRequest) => 
      apiClient.validateTransfer(request),
  });
};
```

---

## Implementation Phases

### Phase 1: Foundation (Q1 2026) - ✅ COMPLETE

**Duration:** Complete  
**Status:** Production-ready

- ✅ Compliance metadata management
- ✅ Compliance indicators API
- ✅ Whitelist management
- ✅ Audit logs (compliance and whitelist)
- ✅ Compliance attestations
- ✅ Attestation packages
- ✅ Compliance reports
- ✅ CSV/JSON exports

### Phase 2: Issuer Management (Q2 2026) - 🔨 RECOMMENDED

**Duration:** 6-8 weeks  
**Priority:** HIGH

**Scope:**
1. Issuer profile data model
2. Issuer profile CRUD endpoints
3. KYB integration (Sumsub)
4. MICA license tracking
5. Issuer assets listing
6. Frontend components for issuer display

**Estimated Effort:**
- Backend: 3 weeks
- Frontend: 2 weeks
- Integration & Testing: 1-2 weeks

### Phase 3: Advanced Access Control (Q3 2026) - 🔨 RECOMMENDED

**Duration:** 8-10 weeks  
**Priority:** HIGH

**Scope:**
1. Blacklist management system
2. Blacklist screening API
3. Enhanced whitelist check endpoint
4. Transfer validation engine
5. Sanctions list integration
6. Real-time compliance screening

**Estimated Effort:**
- Backend: 4 weeks
- Integrations: 2 weeks
- Frontend: 2 weeks
- Testing: 2 weeks

### Phase 4: Regulatory Reporting (Q4 2026) - ⚠️ ENHANCEMENT

**Duration:** 6-8 weeks  
**Priority:** MEDIUM

**Scope:**
1. MICA compliance checklist generator
2. Compliance health dashboard
3. Transfer restrictions query
4. Regulatory alerts system
5. Jurisdiction coverage reports

**Estimated Effort:**
- Backend: 3 weeks
- Frontend: 2 weeks
- Monitoring & Alerts: 2 weeks
- Testing: 1 week

---

## Security and Privacy Considerations

### Data Protection (GDPR Compliance)

| Data Type | Classification | Storage | Retention | Access Control |
|-----------|---------------|---------|-----------|----------------|
| Issuer Legal Name | PII | Encrypted | Indefinite | Admin, Issuer |
| Tax ID | Sensitive PII | Encrypted | 7+ years | Admin only |
| Contact Information | PII | Encrypted | Indefinite | Admin, Issuer |
| Wallet Addresses | Pseudonymous | Plain | Indefinite | Public |
| KYC/KYB Status | Sensitive | Encrypted | 7+ years | Admin, Compliance |
| Audit Logs | Metadata | Plain | 7+ years | Admin, Compliance |

### Authentication and Authorization

#### ARC-0014 Authentication
- All endpoints require valid ARC-0014 transaction signature
- Transaction must be signed by wallet owner
- Signature expires after configurable TTL (default: 5 minutes)

#### Role-Based Access Control (RBAC)

| Role | Permissions |
|------|-------------|
| **User** | Read own compliance data, read whitelist status |
| **Issuer** | Manage own issuer profile, manage own token compliance |
| **Compliance Officer** | Read all compliance data, manage whitelist/blacklist |
| **Admin** | Full access to all data and operations |

### API Security

#### Rate Limiting
- **Compliance Indicators:** 100 requests/minute per user
- **Whitelist Check:** 200 requests/minute per user
- **Blacklist Check:** 200 requests/minute per user
- **Exports:** 10 requests/hour per user
- **Transfer Validation:** 50 requests/minute per user

---

## Testing and Validation

### Test Coverage Requirements
- Minimum 80% code coverage
- 100% coverage for compliance logic
- 100% coverage for security-critical paths

### Performance Testing Targets

| Endpoint | Target RPS | Max Latency (p95) |
|----------|-----------|-------------------|
| Compliance Indicators | 100 | 100ms |
| Whitelist Check | 200 | 50ms |
| Blacklist Check | 200 | 100ms |
| Transfer Validation | 50 | 200ms |
| Compliance Report | 10 | 2000ms |

---

## Appendix A: Complete API Endpoint Summary

### Existing Endpoints (Production)

| Method | Endpoint | Purpose | Status |
|--------|----------|---------|--------|
| GET | `/api/v1/compliance/{assetId}` | Get compliance metadata | ✅ Live |
| POST | `/api/v1/compliance` | Create/update compliance metadata | ✅ Live |
| DELETE | `/api/v1/compliance/{assetId}` | Delete compliance metadata | ✅ Live |
| GET | `/api/v1/compliance/audit-log` | Get compliance audit log | ✅ Live |
| GET | `/api/v1/compliance/attestations` | List attestations | ✅ Live |
| POST | `/api/v1/compliance/attestations` | Create attestation | ✅ Live |
| POST | `/api/v1/compliance/attestation-package` | Generate attestation package | ✅ Live |
| GET | `/api/v1/compliance/report` | Get compliance report | ✅ Live |
| GET | `/api/v1/token/{assetId}/compliance-indicators` | Get compliance indicators | ✅ Live |
| POST | `/api/v1/whitelist` | Add whitelist entry | ✅ Live |
| GET | `/api/v1/whitelist` | List whitelist entries | ✅ Live |
| GET | `/api/v1/whitelist/audit-log` | Get whitelist audit log | ✅ Live |

### Recommended New Endpoints (Roadmap)

| Method | Endpoint | Purpose | Priority | Phase |
|--------|----------|---------|----------|-------|
| GET | `/api/v1/issuer/profile/{address}` | Get issuer profile | **HIGH** | Phase 2 |
| POST | `/api/v1/issuer/profile` | Create/update issuer profile | **HIGH** | Phase 2 |
| GET | `/api/v1/issuer/verification/{address}` | Get issuer verification | **HIGH** | Phase 2 |
| GET | `/api/v1/issuer/{address}/assets` | List issuer assets | Medium | Phase 2 |
| GET | `/api/v1/whitelist/{assetId}/check/{address}` | Check whitelist | **HIGH** | Phase 3 |
| POST | `/api/v1/compliance/blacklist` | Add blacklist entry | **HIGH** | Phase 3 |
| GET | `/api/v1/compliance/blacklist/check` | Check blacklist | **HIGH** | Phase 3 |
| POST | `/api/v1/compliance/validate-transfer` | Validate transfer | **HIGH** | Phase 3 |
| GET | `/api/v1/compliance/{assetId}/mica-checklist` | Get MICA checklist | **HIGH** | Phase 4 |
| GET | `/api/v1/compliance/health` | Get compliance health | Medium | Phase 4 |

---

## Appendix B: MICA Requirements Mapping

| MICA Requirement | API Coverage | Endpoint(s) | Status |
|------------------|--------------|-------------|--------|
| **Art. 35: Issuer identification** | Issuer profile, KYB verification | `/api/v1/issuer/profile` | 🔨 Phase 2 |
| **Art. 36: White paper publication** | Metadata notes field | `/api/v1/compliance` | ✅ Supported |
| **Art. 41: Prudential safeguards** | Compliance status tracking | `/api/v1/compliance/{assetId}` | ✅ Live |
| **Art. 45: Transfer restrictions** | Transfer restrictions, whitelist | `/api/v1/compliance`, `/api/v1/whitelist` | ✅ Live |
| **Art. 59: AML/CTF compliance** | KYC verification, attestations | `/api/v1/compliance/attestations` | ✅ Live |
| **Art. 60: Record keeping (5 years)** | Audit logs (7 year retention) | `/api/v1/compliance/audit-log` | ✅ Live |
| **Art. 76: Reporting to authorities** | Compliance reports, exports | `/api/v1/compliance/report` | ✅ Live |
| **Art. 81: Blacklist enforcement** | Blacklist management | `/api/v1/compliance/blacklist` | 🔨 Phase 3 |
| **Art. 86: Periodic reviews** | Compliance review dates | `/api/v1/compliance/{assetId}` | ✅ Live |

---

## Appendix C: Frontend Readiness Indicators

### Visual Indicators for MICA Dashboards

1. **Compliance Status Badge** - Quick visual compliance status (Compliant, Non-Compliant, Under Review)
2. **MICA Ready Badge** - Indicate MICA regulation compliance (Ready, Incomplete)
3. **Enterprise Readiness Score** - Numeric score (0-100) displayed as progress bar
4. **Issuer Verification Badge** - Show issuer KYB status (Verified, Unverified, Pending)
5. **Whitelist Status Indicator** - Show number of whitelisted addresses
6. **Transfer Restrictions Alert** - Warning icon with restrictions tooltip
7. **Audit Trail Health** - Checkmark indicating audit logging is active
8. **Last Compliance Review Date** - Display recency with warning if > 90 days

---

## Appendix D: Glossary

- **MICA** - Markets in Crypto-Assets Regulation (EU framework)
- **KYC** - Know Your Customer (identity verification for individuals)
- **KYB** - Know Your Business (identity verification for businesses)
- **AML** - Anti-Money Laundering
- **CTF** - Counter-Terrorist Financing
- **OFAC** - Office of Foreign Assets Control (US sanctions authority)
- **RWA** - Real World Asset (tokenized traditional assets)
- **Whitelist** - List of approved addresses that can hold/transfer tokens
- **Blacklist** - List of prohibited addresses blocked from token operations
- **Attestation** - Cryptographic proof of compliance verification
- **Audit Trail** - Immutable log of all compliance-related operations
- **ARC-0014** - Algorand Request for Comments 14 (authentication standard)
- **Enterprise Readiness Score** - Calculated metric (0-100) for compliance maturity

---

## Document Metadata

**Version:** 1.0  
**Authors:** BiatecTokensApi Development Team  
**Last Updated:** January 23, 2026  
**Next Review:** April 23, 2026  

**Related Documents:**
- COMPLIANCE_API.md
- COMPLIANCE_INDICATORS_API.md
- ATTESTATIONS_API_VERIFICATION.md
- AUDIT_LOG_IMPLEMENTATION.md
- VOI_ARAMID_COMPLIANCE_REPORT_API.md
- WHITELIST_FEATURE.md

**Change Log:**
- 2026-01-23: Initial version published

---

**END OF ROADMAP**

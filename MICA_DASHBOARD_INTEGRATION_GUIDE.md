# MICA Dashboard Integration Guide

**Quick Reference for Frontend Teams**  
**Last Updated:** January 23, 2026

---

## Overview

This guide provides frontend developers with quick access to compliance signals APIs for building MICA-compliant dashboards. For the complete API roadmap, see [MICA_COMPLIANCE_SIGNALS_API_ROADMAP.md](./MICA_COMPLIANCE_SIGNALS_API_ROADMAP.md).

---

## Quick Start: Essential APIs

### 1. Get Compliance Status (Production Ready ✅)

```typescript
// Fetch compliance indicators for a token
GET /api/v1/token/{assetId}/compliance-indicators

// Response includes:
// - isMicaReady: boolean
// - enterpriseReadinessScore: 0-100
// - whitelistingEnabled: boolean
// - complianceStatus: string
// - issuerVerificationStatus: enum
```

**Use this for:**
- Dashboard status badges
- Compliance health widgets
- Enterprise readiness scores

### 2. Get Compliance Report (Production Ready ✅)

```typescript
// Fetch comprehensive compliance report
GET /api/v1/compliance/report?network=voimain-v1.0

// Response includes:
// - Compliance metadata
// - Whitelist summary
// - Audit log entries
// - Compliance health score
// - Network-specific status
```

**Use this for:**
- Executive dashboards
- Regulatory reporting
- Compliance monitoring

### 3. Get Attestations (Production Ready ✅)

```typescript
// List compliance attestations
GET /api/v1/compliance/attestations?assetId={assetId}

// Response includes:
// - KYC/AML attestations
// - Verification status
// - Proof hashes (IPFS)
// - Expiration dates
```

**Use this for:**
- Attestation verification
- KYC status display
- Audit trail visualization

---

## Coming Soon: Roadmap Features

### Phase 2: Issuer Management (Q2 2026) 🔨

```typescript
// Get issuer profile (PLANNED)
GET /api/v1/issuer/profile/{issuerAddress}

// Get issuer verification status (PLANNED)
GET /api/v1/issuer/verification/{issuerAddress}
```

**Will enable:**
- Issuer verification badges
- KYB status display
- MICA license tracking

### Phase 3: Advanced Access Control (Q3 2026) 🔨

```typescript
// Check whitelist status (PLANNED)
GET /api/v1/whitelist/{assetId}/check/{address}

// Check blacklist status (PLANNED)
GET /api/v1/compliance/blacklist/check?address={address}

// Validate transfer (PLANNED)
POST /api/v1/compliance/validate-transfer
{
  "assetId": 12345,
  "fromAddress": "...",
  "toAddress": "...",
  "amount": 1000000
}
```

**Will enable:**
- Pre-transfer validation
- Real-time blacklist screening
- Compliance gate for transfers

### Phase 4: Regulatory Reporting (Q4 2026) ⚠️

```typescript
// Get MICA compliance checklist (PLANNED)
GET /api/v1/compliance/{assetId}/mica-checklist

// Get compliance health (PLANNED)
GET /api/v1/compliance/health?issuerAddress={address}
```

**Will enable:**
- MICA compliance checklists
- Aggregate health dashboards
- Regulatory alert systems

---

## Authentication

All endpoints require ARC-0014 authentication:

```typescript
const signedTx = await wallet.signTransaction(authTxn);
const response = await fetch('/api/v1/...', {
  headers: {
    'Authorization': `SigTx ${base64Encode(signedTx)}`
  }
});
```

---

## React/TypeScript Integration

### Install Dependencies

```bash
npm install @tanstack/react-query
```

### Create API Client

```typescript
// lib/compliance-api.ts
export const complianceApi = {
  getIndicators: async (assetId: number) => {
    const response = await authenticatedFetch(
      `/api/v1/token/${assetId}/compliance-indicators`
    );
    return response.json();
  },
  
  getReport: async (network: string) => {
    const response = await authenticatedFetch(
      `/api/v1/compliance/report?network=${network}`
    );
    return response.json();
  },
  
  getAttestations: async (assetId: number) => {
    const response = await authenticatedFetch(
      `/api/v1/compliance/attestations?assetId=${assetId}`
    );
    return response.json();
  }
};
```

### Create React Hooks

```typescript
// hooks/useCompliance.ts
import { useQuery } from '@tanstack/react-query';
import { complianceApi } from '@/lib/compliance-api';

export const useComplianceIndicators = (assetId: number) => {
  return useQuery({
    queryKey: ['compliance-indicators', assetId],
    queryFn: () => complianceApi.getIndicators(assetId),
    staleTime: 5 * 60 * 1000, // Cache for 5 minutes
  });
};

export const useComplianceReport = (network: string) => {
  return useQuery({
    queryKey: ['compliance-report', network],
    queryFn: () => complianceApi.getReport(network),
    staleTime: 10 * 60 * 1000, // Cache for 10 minutes
  });
};
```

### Build UI Components

```typescript
// components/ComplianceStatusBadge.tsx
import { useComplianceIndicators } from '@/hooks/useCompliance';

export const ComplianceStatusBadge = ({ assetId }: { assetId: number }) => {
  const { data: indicators, isLoading } = useComplianceIndicators(assetId);
  
  if (isLoading) return <Skeleton className="h-6 w-24" />;
  
  return (
    <Badge variant={indicators.isMicaReady ? 'success' : 'warning'}>
      {indicators.isMicaReady ? '✓ MICA Ready' : '⚠ MICA Incomplete'}
    </Badge>
  );
};

// components/EnterpriseReadinessScore.tsx
export const EnterpriseReadinessScore = ({ assetId }: { assetId: number }) => {
  const { data: indicators } = useComplianceIndicators(assetId);
  
  const getScoreColor = (score: number) => {
    if (score >= 80) return 'green';
    if (score >= 60) return 'yellow';
    if (score >= 40) return 'orange';
    return 'red';
  };
  
  return (
    <div className="space-y-2">
      <Label>Enterprise Readiness</Label>
      <Progress 
        value={indicators.enterpriseReadinessScore} 
        className={`bg-${getScoreColor(indicators.enterpriseReadinessScore)}-500`}
      />
      <Text>{indicators.enterpriseReadinessScore}/100</Text>
    </div>
  );
};
```

---

## UI Components Library

### Recommended Components

1. **ComplianceStatusBadge** - Shows MICA ready status
2. **EnterpriseReadinessScore** - Progress bar with score
3. **IssuerVerificationBadge** - Shows issuer KYB status  
4. **WhitelistStatusCard** - Displays whitelist info
5. **AuditTrailTimeline** - Shows recent audit events
6. **ComplianceHealthDashboard** - Aggregate view
7. **AttestationList** - Lists KYC/AML attestations
8. **TransferValidator** - Pre-transfer checks (coming soon)

### Design System Recommendations

**Colors:**
- ✅ **Green**: Compliant, verified, approved
- ⚠️ **Yellow**: Under review, pending, warnings
- ❌ **Red**: Non-compliant, failed, denied
- ⏳ **Blue**: In progress, processing

**Icons:**
- Shield checkmark: Verified
- Shield warning: Partially verified
- Shield X: Unverified
- Clock: Pending
- Alert triangle: Warning
- Lock: Restricted

---

## Performance Optimization

### Caching Strategy

```typescript
// Recommended cache times
const CACHE_TIMES = {
  complianceIndicators: 5 * 60 * 1000,      // 5 minutes
  complianceReport: 10 * 60 * 1000,         // 10 minutes
  issuerProfile: 30 * 60 * 1000,            // 30 minutes
  whitelistCheck: 60 * 1000,                // 1 minute
  blacklistCheck: 5 * 60 * 1000,            // 5 minutes
  attestations: 15 * 60 * 1000,             // 15 minutes
};
```

### Rate Limiting

Be aware of API rate limits:
- Compliance Indicators: 100 req/min
- Compliance Report: 10 req/min
- Attestations: 50 req/min
- Exports: 10 req/hour

**Best Practice:** Use React Query's caching to minimize API calls.

---

## Error Handling

```typescript
export const useComplianceIndicators = (assetId: number) => {
  return useQuery({
    queryKey: ['compliance-indicators', assetId],
    queryFn: () => complianceApi.getIndicators(assetId),
    retry: 3,
    retryDelay: (attemptIndex) => Math.min(1000 * 2 ** attemptIndex, 30000),
    onError: (error) => {
      console.error('Failed to fetch compliance indicators:', error);
      toast.error('Unable to load compliance data. Please try again.');
    }
  });
};
```

---

## Testing

### Mock Data for Development

```typescript
// mocks/compliance.ts
export const mockComplianceIndicators = {
  assetId: 12345,
  isMicaReady: true,
  whitelistingEnabled: true,
  whitelistedAddressCount: 150,
  hasTransferRestrictions: true,
  transferRestrictions: "KYC required; accredited investors only",
  requiresAccreditedInvestors: true,
  complianceStatus: "Compliant",
  verificationStatus: "Verified",
  regulatoryFramework: "MICA, SEC Reg D",
  jurisdiction: "EU, US",
  maxHolders: 100,
  enterpriseReadinessScore: 95,
  network: "voimain-v1.0",
  hasComplianceMetadata: true,
  lastComplianceUpdate: "2026-01-23T12:00:00Z"
};
```

### Unit Tests

```typescript
// __tests__/ComplianceStatusBadge.test.tsx
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ComplianceStatusBadge } from '@/components/ComplianceStatusBadge';

test('shows MICA Ready badge for compliant token', async () => {
  const queryClient = new QueryClient();
  
  render(
    <QueryClientProvider client={queryClient}>
      <ComplianceStatusBadge assetId={12345} />
    </QueryClientProvider>
  );
  
  expect(await screen.findByText('✓ MICA Ready')).toBeInTheDocument();
});
```

---

## Resources

### Documentation
- **Full Roadmap:** [MICA_COMPLIANCE_SIGNALS_API_ROADMAP.md](./MICA_COMPLIANCE_SIGNALS_API_ROADMAP.md)
- **Compliance API:** [COMPLIANCE_API.md](./COMPLIANCE_API.md)
- **Compliance Indicators:** [COMPLIANCE_INDICATORS_API.md](./COMPLIANCE_INDICATORS_API.md)
- **Attestations API:** [ATTESTATIONS_API_VERIFICATION.md](./ATTESTATIONS_API_VERIFICATION.md)
- **Audit Logs:** [AUDIT_LOG_IMPLEMENTATION.md](./AUDIT_LOG_IMPLEMENTATION.md)
- **Whitelist Feature:** [WHITELIST_FEATURE.md](./WHITELIST_FEATURE.md)

### Support
- Repository: https://github.com/scholtz/BiatecTokensApi
- API Documentation: Available at `/swagger` endpoint
- Issues: https://github.com/scholtz/BiatecTokensApi/issues

---

## Quick Reference Card

| What You Need | Use This API | Status |
|---------------|--------------|--------|
| Token compliance status | `GET /token/{id}/compliance-indicators` | ✅ Live |
| Comprehensive report | `GET /compliance/report` | ✅ Live |
| KYC attestations | `GET /compliance/attestations` | ✅ Live |
| Audit trail | `GET /compliance/audit-log` | ✅ Live |
| Whitelist entries | `GET /whitelist` | ✅ Live |
| Issuer profile | `GET /issuer/profile/{address}` | 🔨 Q2 2026 |
| Blacklist check | `GET /compliance/blacklist/check` | 🔨 Q3 2026 |
| Transfer validation | `POST /compliance/validate-transfer` | 🔨 Q3 2026 |
| MICA checklist | `GET /compliance/{id}/mica-checklist` | 🔨 Q4 2026 |

---

**Last Updated:** January 23, 2026  
**Version:** 1.0  
**For the complete roadmap, see:** [MICA_COMPLIANCE_SIGNALS_API_ROADMAP.md](./MICA_COMPLIANCE_SIGNALS_API_ROADMAP.md)

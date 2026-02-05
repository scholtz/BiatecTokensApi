# Subscription API Guide

## Overview

The BiatecTokensApi provides a complete Stripe-based subscription system for managing paid access to the regulated token issuance platform. This guide covers all subscription-related endpoints, webhook integration, and error handling patterns.

## Table of Contents

1. [Subscription Tiers](#subscription-tiers)
2. [API Endpoints](#api-endpoints)
3. [Webhook Integration](#webhook-integration)
4. [Error Handling](#error-handling)
5. [Frontend Integration](#frontend-integration)
6. [Testing](#testing)

## Subscription Tiers

The platform offers four subscription tiers with different feature sets and limits:

### Free Tier
- **Cost**: Free
- **Token Deployments**: 1 per month
- **Whitelisted Addresses**: 10 per token
- **Compliance Reports**: 1 per month
- **Advanced Features**: None
- **API Access**: Yes
- **Webhooks**: No
- **Audit Exports**: No
- **SLA**: No

### Basic Tier ($29/month)
- **Cost**: $29/month
- **Token Deployments**: 10 per month
- **Whitelisted Addresses**: 100 per token
- **Compliance Reports**: 10 per month
- **Advanced Compliance**: Yes
- **API Access**: Yes
- **Webhooks**: Yes
- **Audit Exports**: 5 per month
- **SLA**: No

### Premium Tier ($99/month)
- **Cost**: $99/month
- **Token Deployments**: 100 per month
- **Whitelisted Addresses**: 1,000 per token
- **Compliance Reports**: 100 per month
- **Advanced Compliance**: Yes
- **Multi-Jurisdiction**: Yes
- **Custom Branding**: Yes
- **Priority Support**: Yes
- **API Access**: Yes
- **Webhooks**: Yes
- **Audit Exports**: 50 per month
- **SLA**: 99.5% uptime

### Enterprise Tier ($299/month)
- **Cost**: $299/month
- **Token Deployments**: Unlimited
- **Whitelisted Addresses**: Unlimited
- **Compliance Reports**: Unlimited
- **All Features**: Yes
- **Priority Support**: Yes
- **Audit Exports**: Unlimited
- **SLA**: 99.9% uptime
- **Dedicated Support**: Yes

## API Endpoints

All subscription endpoints require ARC-0014 authentication (except webhooks).

### GET /api/v1/subscription/status

Retrieves the current subscription status for the authenticated user.

**Authentication**: Required (ARC-0014)

**Response**:
```json
{
  "success": true,
  "subscription": {
    "userAddress": "ALGORAND_ADDRESS",
    "stripeCustomerId": "cus_...",
    "stripeSubscriptionId": "sub_...",
    "tier": "Premium",
    "status": "Active",
    "subscriptionStartDate": "2026-01-01T00:00:00Z",
    "currentPeriodStart": "2026-02-01T00:00:00Z",
    "currentPeriodEnd": "2026-03-01T00:00:00Z",
    "cancelAtPeriodEnd": false,
    "paymentFailureCount": 0,
    "lastUpdated": "2026-02-05T07:18:00Z"
  }
}
```

**Status Enum Values**:
- `None`: No subscription
- `Active`: Subscription is active
- `PastDue`: Payment failed, grace period
- `Unpaid`: Payment failed, no access
- `Canceled`: Subscription canceled
- `Incomplete`: Subscription incomplete (pending payment)
- `Trialing`: In trial period
- `Paused`: Subscription paused

### GET /api/v1/subscription/entitlements

Gets the feature entitlements for the authenticated user's subscription tier.

**Authentication**: Required (ARC-0014)

**Response**:
```json
{
  "success": true,
  "entitlements": {
    "tier": "Premium",
    "maxTokenDeployments": 100,
    "maxWhitelistedAddresses": 1000,
    "maxComplianceReports": 100,
    "advancedComplianceEnabled": true,
    "multiJurisdictionEnabled": true,
    "customBrandingEnabled": true,
    "prioritySupportEnabled": true,
    "apiAccessEnabled": true,
    "webhooksEnabled": true,
    "auditExportsEnabled": true,
    "maxAuditExports": 50,
    "slaEnabled": true,
    "slaUptimePercentage": 99.5
  }
}
```

**Note**: A value of `-1` indicates unlimited for that feature.

### POST /api/v1/subscription/checkout

Creates a Stripe checkout session for purchasing a subscription.

**Authentication**: Required (ARC-0014)

**Request Body**:
```json
{
  "tier": "Premium"
}
```

**Tier Values**: `Basic`, `Premium`, `Enterprise` (cannot purchase `Free`)

**Response**:
```json
{
  "success": true,
  "sessionId": "cs_test_...",
  "checkoutUrl": "https://checkout.stripe.com/c/pay/..."
}
```

**Error Response**:
```json
{
  "success": false,
  "errorMessage": "Cannot create checkout session for Free tier"
}
```

**Usage**:
1. Call this endpoint with the desired tier
2. Redirect user to `checkoutUrl`
3. User completes payment on Stripe
4. User is redirected to your success URL
5. Webhook updates subscription status

### POST /api/v1/subscription/billing-portal

Creates a Stripe billing portal session for managing an existing subscription.

**Authentication**: Required (ARC-0014)

**Request Body** (optional):
```json
{
  "returnUrl": "https://tokens.biatec.io/subscription"
}
```

**Response**:
```json
{
  "success": true,
  "portalUrl": "https://billing.stripe.com/p/session/..."
}
```

**Error Response**:
```json
{
  "success": false,
  "errorMessage": "No active subscription found"
}
```

**Portal Capabilities**:
- View subscription details and billing history
- Update payment methods
- Upgrade or downgrade subscription tier
- Cancel subscription
- Download invoices

### POST /api/v1/subscription/webhook

Webhook endpoint for processing Stripe events.

**Authentication**: None (uses Stripe signature validation)

**Headers**:
```
Stripe-Signature: t=timestamp,v1=signature
```

**Supported Events**:
- `checkout.session.completed`: Checkout completed
- `customer.subscription.created`: New subscription created
- `customer.subscription.updated`: Subscription modified
- `customer.subscription.deleted`: Subscription canceled
- `invoice.payment_succeeded`: Invoice payment successful
- `invoice.payment_failed`: Invoice payment failed
- `charge.dispute.created`: Payment dispute initiated

**Response**:
- 200 OK: Event processed successfully
- 400 Bad Request: Invalid signature or event processing failed

**Configuration**:
Configure this webhook URL in your Stripe dashboard:
```
https://api.tokens.biatec.io/api/v1/subscription/webhook
```

## Webhook Integration

### Webhook Security

All webhooks are validated using Stripe signature verification. Configure the webhook secret in your application configuration:

```json
{
  "StripeConfig": {
    "WebhookSecret": "whsec_..."
  }
}
```

### Webhook Event Processing

Events are processed idempotently using the Stripe event ID. Duplicate events are safely ignored.

### Event Handling

#### checkout.session.completed
- Updates or creates Stripe customer ID
- Prepares for subscription activation

#### customer.subscription.created
- Activates new subscription
- Sets subscription tier
- Updates entitlements

#### customer.subscription.updated
- Updates subscription status
- Handles tier changes
- Manages cancelation flags

#### customer.subscription.deleted
- Marks subscription as canceled
- Reverts to Free tier
- Sets end date

#### invoice.payment_succeeded
- Resets payment failure counters
- Confirms successful billing

#### invoice.payment_failed
- Increments failure counter
- Records failure reason
- Triggers retry logic

#### charge.dispute.created
- Marks active dispute
- Records dispute date
- Notifies administrators

## Error Handling

All API errors follow a standardized format with error codes.

### Error Response Format

```json
{
  "success": false,
  "errorCode": "SUBSCRIPTION_EXPIRED",
  "errorMessage": "Your subscription has expired. Please renew to continue.",
  "timestamp": "2026-02-05T07:18:00Z",
  "path": "/api/v1/subscription/status",
  "correlationId": "abc-123-def",
  "remediationHint": "Visit the billing portal to renew your subscription"
}
```

### Subscription Error Codes

| Error Code | HTTP Status | Description | Remediation |
|------------|-------------|-------------|-------------|
| `SUBSCRIPTION_NOT_FOUND` | 404 | No subscription found | Create a subscription |
| `SUBSCRIPTION_EXPIRED` | 403 | Subscription has expired | Renew subscription |
| `SUBSCRIPTION_PAST_DUE` | 402 | Payment failed, in grace period | Update payment method |
| `PAYMENT_FAILED` | 402 | Payment failed | Update payment method |
| `PAYMENT_METHOD_REQUIRED` | 402 | Payment method needed | Add payment method |
| `SUBSCRIPTION_HAS_DISPUTE` | 403 | Active payment dispute | Resolve dispute |
| `FEATURE_NOT_AVAILABLE` | 403 | Feature not in current tier | Upgrade subscription |
| `UPGRADE_REQUIRED` | 403 | Upgrade needed for feature | Upgrade subscription |
| `CANNOT_PURCHASE_FREE_TIER` | 400 | Cannot checkout Free tier | Select paid tier |
| `STRIPE_SERVICE_ERROR` | 502 | Stripe API error | Try again later |
| `WEBHOOK_SIGNATURE_INVALID` | 400 | Invalid webhook signature | Check configuration |
| `PRICE_NOT_CONFIGURED` | 500 | Tier price not configured | Contact support |

## Frontend Integration

### Basic Flow

1. **Check Subscription Status**
   ```javascript
   const response = await fetch('/api/v1/subscription/status', {
     headers: {
       'Authorization': `SigTx ${signedTransaction}`
     }
   });
   const { subscription } = await response.json();
   ```

2. **Get Entitlements**
   ```javascript
   const response = await fetch('/api/v1/subscription/entitlements', {
     headers: {
       'Authorization': `SigTx ${signedTransaction}`
     }
   });
   const { entitlements } = await response.json();
   
   // Use entitlements for feature gating
   if (entitlements.customBrandingEnabled) {
     // Show custom branding options
   }
   ```

3. **Create Checkout**
   ```javascript
   const response = await fetch('/api/v1/subscription/checkout', {
     method: 'POST',
     headers: {
       'Authorization': `SigTx ${signedTransaction}`,
       'Content-Type': 'application/json'
     },
     body: JSON.stringify({ tier: 'Premium' })
   });
   const { checkoutUrl } = await response.json();
   
   // Redirect to Stripe checkout
   window.location.href = checkoutUrl;
   ```

4. **Open Billing Portal**
   ```javascript
   const response = await fetch('/api/v1/subscription/billing-portal', {
     method: 'POST',
     headers: {
       'Authorization': `SigTx ${signedTransaction}`,
       'Content-Type': 'application/json'
     },
     body: JSON.stringify({ 
       returnUrl: window.location.href 
     })
   });
   const { portalUrl } = await response.json();
   
   // Redirect to Stripe billing portal
   window.location.href = portalUrl;
   ```

### Feature Gating Example

```javascript
// Check if user can deploy token
async function canDeployToken() {
  const { entitlements } = await getEntitlements();
  const currentUsage = await getUsage();
  
  if (entitlements.maxTokenDeployments === -1) {
    return true; // Unlimited
  }
  
  if (currentUsage.tokenIssuanceCount >= entitlements.maxTokenDeployments) {
    showUpgradePrompt();
    return false;
  }
  
  return true;
}
```

### Handling Payment Failures

```javascript
const { subscription } = await getSubscriptionStatus();

if (subscription.status === 'PastDue' || subscription.paymentFailureCount > 0) {
  // Show payment failure banner
  showBanner({
    type: 'warning',
    message: 'Your payment failed. Please update your payment method.',
    action: 'Update Payment',
    onClick: () => openBillingPortal()
  });
}
```

## Testing

### Stripe Test Mode

Use Stripe test mode for development and testing.

**Test Card Numbers**:
- Success: `4242 4242 4242 4242`
- Decline: `4000 0000 0000 0002`
- Requires Authentication: `4000 0025 0000 3155`

**Test Configuration**:
```json
{
  "StripeConfig": {
    "SecretKey": "sk_test_...",
    "PublishableKey": "pk_test_...",
    "WebhookSecret": "whsec_test_...",
    "BasicPriceId": "price_test_...",
    "ProPriceId": "price_test_...",
    "EnterprisePriceId": "price_test_..."
  }
}
```

### Manual Testing Checklist

- [ ] Create checkout session
- [ ] Complete payment with test card
- [ ] Verify subscription status updates
- [ ] Test billing portal access
- [ ] Test tier upgrade
- [ ] Test tier downgrade
- [ ] Test subscription cancellation
- [ ] Test payment failure
- [ ] Test webhook processing
- [ ] Test entitlements endpoint
- [ ] Test feature gating

### Webhook Testing

Use Stripe CLI to test webhooks locally:

```bash
stripe listen --forward-to localhost:7000/api/v1/subscription/webhook
stripe trigger checkout.session.completed
stripe trigger customer.subscription.created
stripe trigger invoice.payment_failed
```

## Best Practices

1. **Always check entitlements** before allowing feature access
2. **Handle payment failures gracefully** with clear user messaging
3. **Use billing portal** for subscription management (don't build custom UI)
4. **Monitor webhook processing** for failures and retries
5. **Test thoroughly** in Stripe test mode before going live
6. **Implement proper error handling** for all subscription operations
7. **Cache entitlements** for performance (refresh on subscription changes)
8. **Log all subscription events** for audit and debugging
9. **Use correlation IDs** for request tracing
10. **Implement retry logic** for transient failures

## Support

For issues or questions:
- Check the [Health Status Endpoint](#health-check): `/api/v1/status`
- Review error codes and remediation hints
- Contact support@biatec.io
- Submit issues on GitHub

## Version History

- **v1.0.0** (2026-02-05): Initial subscription system release
  - Basic, Premium, Enterprise tiers
  - Stripe integration
  - Webhook processing
  - Entitlements API

using BiatecTokensApi.Configuration;
using BiatecTokensApi.Models.Subscription;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Service for managing Stripe subscription lifecycle
    /// </summary>
    public class StripeService : IStripeService
    {
        private readonly StripeConfig _config;
        private readonly ISubscriptionRepository _subscriptionRepository;
        private readonly ISubscriptionTierService _tierService;
        private readonly ILogger<StripeService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="StripeService"/> class.
        /// </summary>
        /// <param name="config">Stripe configuration</param>
        /// <param name="subscriptionRepository">Subscription repository</param>
        /// <param name="tierService">Subscription tier service</param>
        /// <param name="logger">Logger instance</param>
        public StripeService(
            IOptions<StripeConfig> config,
            ISubscriptionRepository subscriptionRepository,
            ISubscriptionTierService tierService,
            ILogger<StripeService> logger)
        {
            _config = config.Value;
            _subscriptionRepository = subscriptionRepository;
            _tierService = tierService;
            _logger = logger;

            // Set Stripe API key
            StripeConfiguration.ApiKey = _config.SecretKey;
        }

        /// <inheritdoc/>
        public async Task<CreateCheckoutSessionResponse> CreateCheckoutSessionAsync(string userAddress, SubscriptionTier tier)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(userAddress))
                {
                    throw new ArgumentException("User address is required", nameof(userAddress));
                }

                if (tier == SubscriptionTier.Free)
                {
                    return new CreateCheckoutSessionResponse
                    {
                        Success = false,
                        ErrorMessage = "Cannot create checkout session for Free tier"
                    };
                }

                // Get or create subscription state
                var subscription = await _subscriptionRepository.GetSubscriptionAsync(userAddress);
                if (subscription == null)
                {
                    subscription = new SubscriptionState
                    {
                        UserAddress = userAddress,
                        Tier = SubscriptionTier.Free,
                        Status = SubscriptionStatus.None
                    };
                }

                // Get price ID based on tier
                var priceId = GetPriceIdForTier(tier);
                if (string.IsNullOrWhiteSpace(priceId))
                {
                    return new CreateCheckoutSessionResponse
                    {
                        Success = false,
                        ErrorMessage = $"Price ID not configured for tier: {tier}"
                    };
                }

                // Create or get Stripe customer
                var customerId = subscription.StripeCustomerId;
                if (string.IsNullOrWhiteSpace(customerId))
                {
                    var customerService = new CustomerService();
                    var customer = await customerService.CreateAsync(new CustomerCreateOptions
                    {
                        Metadata = new Dictionary<string, string>
                        {
                            { "algorand_address", userAddress }
                        }
                    });
                    customerId = customer.Id;

                    // Save customer ID
                    subscription.StripeCustomerId = customerId;
                    await _subscriptionRepository.SaveSubscriptionAsync(subscription);
                }

                // Create checkout session
                var sessionService = new SessionService();
                var options = new SessionCreateOptions
                {
                    Customer = customerId,
                    PaymentMethodTypes = new List<string> { "card" },
                    LineItems = new List<SessionLineItemOptions>
                    {
                        new SessionLineItemOptions
                        {
                            Price = priceId,
                            Quantity = 1
                        }
                    },
                    Mode = "subscription",
                    SuccessUrl = _config.CheckoutSuccessUrl,
                    CancelUrl = _config.CheckoutCancelUrl,
                    Metadata = new Dictionary<string, string>
                    {
                        { "algorand_address", userAddress },
                        { "tier", tier.ToString() }
                    }
                };

                var session = await sessionService.CreateAsync(options);

                _logger.LogInformation(
                    "Created checkout session {SessionId} for user {UserAddress}, tier {Tier}",
                    session.Id, userAddress, tier);

                return new CreateCheckoutSessionResponse
                {
                    Success = true,
                    SessionId = session.Id,
                    CheckoutUrl = session.Url
                };
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Stripe error creating checkout session for user {UserAddress}", userAddress);
                return new CreateCheckoutSessionResponse
                {
                    Success = false,
                    ErrorMessage = $"Payment service error: {ex.Message}"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating checkout session for user {UserAddress}", userAddress);
                return new CreateCheckoutSessionResponse
                {
                    Success = false,
                    ErrorMessage = "An error occurred while creating checkout session"
                };
            }
        }

        /// <inheritdoc/>
        public async Task<CreateBillingPortalSessionResponse> CreateBillingPortalSessionAsync(string userAddress, string? returnUrl = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(userAddress))
                {
                    throw new ArgumentException("User address is required", nameof(userAddress));
                }

                var subscription = await _subscriptionRepository.GetSubscriptionAsync(userAddress);
                if (subscription == null || string.IsNullOrWhiteSpace(subscription.StripeCustomerId))
                {
                    return new CreateBillingPortalSessionResponse
                    {
                        Success = false,
                        ErrorMessage = "No active subscription found"
                    };
                }

                var sessionService = new Stripe.BillingPortal.SessionService();
                var options = new Stripe.BillingPortal.SessionCreateOptions
                {
                    Customer = subscription.StripeCustomerId,
                    ReturnUrl = returnUrl ?? _config.CheckoutSuccessUrl
                };

                var session = await sessionService.CreateAsync(options);

                _logger.LogInformation(
                    "Created billing portal session for user {UserAddress}, customer {CustomerId}",
                    userAddress, subscription.StripeCustomerId);

                return new CreateBillingPortalSessionResponse
                {
                    Success = true,
                    PortalUrl = session.Url
                };
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Stripe error creating billing portal session for user {UserAddress}", userAddress);
                return new CreateBillingPortalSessionResponse
                {
                    Success = false,
                    ErrorMessage = $"Payment service error: {ex.Message}"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating billing portal session for user {UserAddress}", userAddress);
                return new CreateBillingPortalSessionResponse
                {
                    Success = false,
                    ErrorMessage = "An error occurred while creating billing portal session"
                };
            }
        }

        /// <inheritdoc/>
        public async Task<SubscriptionState> GetSubscriptionStatusAsync(string userAddress)
        {
            if (string.IsNullOrWhiteSpace(userAddress))
            {
                throw new ArgumentException("User address is required", nameof(userAddress));
            }

            var subscription = await _subscriptionRepository.GetSubscriptionAsync(userAddress);
            if (subscription == null)
            {
                subscription = new SubscriptionState
                {
                    UserAddress = userAddress,
                    Tier = SubscriptionTier.Free,
                    Status = SubscriptionStatus.None
                };
            }

            return subscription;
        }

        /// <inheritdoc/>
        public async Task<bool> ProcessWebhookEventAsync(string json, string signature)
        {
            try
            {
                // Verify webhook signature
                var stripeEvent = EventUtility.ConstructEvent(
                    json,
                    signature,
                    _config.WebhookSecret
                );

                // Check idempotency
                if (await _subscriptionRepository.IsEventProcessedAsync(stripeEvent.Id))
                {
                    _logger.LogInformation("Webhook event {EventId} already processed, skipping", stripeEvent.Id);
                    return true;
                }

                _logger.LogInformation(
                    "Processing webhook event {EventId}, type {EventType}",
                    stripeEvent.Id, stripeEvent.Type);

                // Process based on event type
                var processed = stripeEvent.Type switch
                {
                    "checkout.session.completed" => await HandleCheckoutCompletedAsync(stripeEvent),
                    "customer.subscription.created" => await HandleSubscriptionCreatedAsync(stripeEvent),
                    "customer.subscription.updated" => await HandleSubscriptionUpdatedAsync(stripeEvent),
                    "customer.subscription.deleted" => await HandleSubscriptionDeletedAsync(stripeEvent),
                    "invoice.payment_succeeded" => await HandleInvoicePaymentSucceededAsync(stripeEvent),
                    "invoice.payment_failed" => await HandleInvoicePaymentFailedAsync(stripeEvent),
                    "charge.dispute.created" => await HandleDisputeCreatedAsync(stripeEvent),
                    _ => await HandleUnknownEventAsync(stripeEvent)
                };

                return processed;
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Stripe error processing webhook: {Message}", ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing webhook");
                return false;
            }
        }

        private async Task<bool> HandleCheckoutCompletedAsync(Event stripeEvent)
        {
            var session = stripeEvent.Data.Object as Session;
            if (session == null)
            {
                _logger.LogWarning("Checkout session data is null in event {EventId}", stripeEvent.Id);
                return false;
            }

            var userAddress = session.Metadata?.GetValueOrDefault("algorand_address");
            if (string.IsNullOrWhiteSpace(userAddress))
            {
                _logger.LogWarning("No user address in checkout session metadata, event {EventId}", stripeEvent.Id);
                return false;
            }

            // Update customer ID if needed
            var subscription = await _subscriptionRepository.GetSubscriptionAsync(userAddress);
            if (subscription == null)
            {
                subscription = new SubscriptionState
                {
                    UserAddress = userAddress,
                    Tier = SubscriptionTier.Free,
                    Status = SubscriptionStatus.None
                };
            }

            if (string.IsNullOrWhiteSpace(subscription.StripeCustomerId))
            {
                subscription.StripeCustomerId = session.CustomerId;
                await _subscriptionRepository.SaveSubscriptionAsync(subscription);
            }

            await _subscriptionRepository.MarkEventProcessedAsync(new SubscriptionWebhookEvent
            {
                EventId = stripeEvent.Id,
                EventType = stripeEvent.Type,
                UserAddress = userAddress,
                StripeSubscriptionId = session.SubscriptionId,
                Tier = subscription.Tier,
                Status = subscription.Status,
                Success = true
            });

            _logger.LogInformation("Processed checkout completed for user {UserAddress}", userAddress);
            return true;
        }

        private async Task<bool> HandleSubscriptionCreatedAsync(Event stripeEvent)
        {
            var stripeSubscription = stripeEvent.Data.Object as Subscription;
            if (stripeSubscription == null)
            {
                _logger.LogWarning("Subscription data is null in event {EventId}", stripeEvent.Id);
                return false;
            }

            var subscription = await GetOrCreateSubscriptionFromStripeAsync(stripeSubscription);
            if (subscription == null)
            {
                return false;
            }

            UpdateSubscriptionFromStripe(subscription, stripeSubscription);
            await _subscriptionRepository.SaveSubscriptionAsync(subscription);

            // Update tier in tier service
            if (_tierService is SubscriptionTierService tierService)
            {
                tierService.SetUserTier(subscription.UserAddress, subscription.Tier);
            }

            await _subscriptionRepository.MarkEventProcessedAsync(new SubscriptionWebhookEvent
            {
                EventId = stripeEvent.Id,
                EventType = stripeEvent.Type,
                UserAddress = subscription.UserAddress,
                StripeSubscriptionId = subscription.StripeSubscriptionId,
                Tier = subscription.Tier,
                Status = subscription.Status,
                Success = true
            });

            _logger.LogInformation(
                "Processed subscription created for user {UserAddress}, tier {Tier}",
                subscription.UserAddress, subscription.Tier);

            return true;
        }

        private async Task<bool> HandleSubscriptionUpdatedAsync(Event stripeEvent)
        {
            var stripeSubscription = stripeEvent.Data.Object as Subscription;
            if (stripeSubscription == null)
            {
                _logger.LogWarning("Subscription data is null in event {EventId}", stripeEvent.Id);
                return false;
            }

            var subscription = await GetOrCreateSubscriptionFromStripeAsync(stripeSubscription);
            if (subscription == null)
            {
                return false;
            }

            UpdateSubscriptionFromStripe(subscription, stripeSubscription);
            await _subscriptionRepository.SaveSubscriptionAsync(subscription);

            // Update tier in tier service
            if (_tierService is SubscriptionTierService tierService)
            {
                tierService.SetUserTier(subscription.UserAddress, subscription.Tier);
            }

            await _subscriptionRepository.MarkEventProcessedAsync(new SubscriptionWebhookEvent
            {
                EventId = stripeEvent.Id,
                EventType = stripeEvent.Type,
                UserAddress = subscription.UserAddress,
                StripeSubscriptionId = subscription.StripeSubscriptionId,
                Tier = subscription.Tier,
                Status = subscription.Status,
                Success = true
            });

            _logger.LogInformation(
                "Processed subscription updated for user {UserAddress}, tier {Tier}, status {Status}",
                subscription.UserAddress, subscription.Tier, subscription.Status);

            return true;
        }

        private async Task<bool> HandleSubscriptionDeletedAsync(Event stripeEvent)
        {
            var stripeSubscription = stripeEvent.Data.Object as Subscription;
            if (stripeSubscription == null)
            {
                _logger.LogWarning("Subscription data is null in event {EventId}", stripeEvent.Id);
                return false;
            }

            var subscription = await _subscriptionRepository.GetSubscriptionBySubscriptionIdAsync(stripeSubscription.Id);
            if (subscription == null)
            {
                _logger.LogWarning("Subscription not found for Stripe subscription {SubscriptionId}", stripeSubscription.Id);
                return false;
            }

            subscription.Status = SubscriptionStatus.Canceled;
            subscription.Tier = SubscriptionTier.Free;
            subscription.SubscriptionEndDate = DateTime.UtcNow;
            await _subscriptionRepository.SaveSubscriptionAsync(subscription);

            // Update tier in tier service back to Free
            if (_tierService is SubscriptionTierService tierService)
            {
                tierService.SetUserTier(subscription.UserAddress, SubscriptionTier.Free);
            }

            await _subscriptionRepository.MarkEventProcessedAsync(new SubscriptionWebhookEvent
            {
                EventId = stripeEvent.Id,
                EventType = stripeEvent.Type,
                UserAddress = subscription.UserAddress,
                StripeSubscriptionId = subscription.StripeSubscriptionId,
                Tier = subscription.Tier,
                Status = subscription.Status,
                Success = true
            });

            _logger.LogInformation(
                "Processed subscription deleted for user {UserAddress}",
                subscription.UserAddress);

            return true;
        }

        private async Task<bool> HandleUnknownEventAsync(Event stripeEvent)
        {
            _logger.LogInformation("Unhandled webhook event type: {EventType}", stripeEvent.Type);

            // Still mark as processed to avoid re-processing
            await _subscriptionRepository.MarkEventProcessedAsync(new SubscriptionWebhookEvent
            {
                EventId = stripeEvent.Id,
                EventType = stripeEvent.Type,
                UserAddress = string.Empty,
                Success = true
            });

            return true;
        }

        private async Task<SubscriptionState?> GetOrCreateSubscriptionFromStripeAsync(Subscription stripeSubscription)
        {
            // Try to find by subscription ID first
            var subscription = await _subscriptionRepository.GetSubscriptionBySubscriptionIdAsync(stripeSubscription.Id);
            if (subscription != null)
            {
                return subscription;
            }

            // Try to find by customer ID
            subscription = await _subscriptionRepository.GetSubscriptionByCustomerIdAsync(stripeSubscription.CustomerId);
            if (subscription != null)
            {
                return subscription;
            }

            // Try to get user address from customer metadata
            var customerService = new CustomerService();
            var customer = await customerService.GetAsync(stripeSubscription.CustomerId);
            var userAddress = customer.Metadata?.GetValueOrDefault("algorand_address");

            if (string.IsNullOrWhiteSpace(userAddress))
            {
                _logger.LogWarning(
                    "Cannot determine user address for subscription {SubscriptionId}, customer {CustomerId}",
                    stripeSubscription.Id, stripeSubscription.CustomerId);
                return null;
            }

            // Create new subscription state
            subscription = new SubscriptionState
            {
                UserAddress = userAddress,
                StripeCustomerId = stripeSubscription.CustomerId,
                Tier = SubscriptionTier.Free,
                Status = SubscriptionStatus.None
            };

            return subscription;
        }

        private void UpdateSubscriptionFromStripe(SubscriptionState subscription, Subscription stripeSubscription)
        {
            subscription.StripeSubscriptionId = stripeSubscription.Id;
            subscription.Status = MapStripeStatus(stripeSubscription.Status);
            subscription.CancelAtPeriodEnd = stripeSubscription.CancelAtPeriodEnd;
            
            // Stripe.net uses DateTime? for these properties
            subscription.CurrentPeriodStart = stripeSubscription.Created;
            subscription.CurrentPeriodEnd = null; // Will be updated from subscription object

            if (subscription.SubscriptionStartDate == null)
            {
                subscription.SubscriptionStartDate = stripeSubscription.Created;
            }

            // Determine tier from price ID
            if (stripeSubscription.Items?.Data != null && stripeSubscription.Items.Data.Any())
            {
                var priceId = stripeSubscription.Items.Data.First().Price?.Id;
                if (!string.IsNullOrWhiteSpace(priceId))
                {
                    subscription.Tier = GetTierFromPriceId(priceId);
                }
            }

            // Only keep subscription active if status allows
            if (subscription.Status != SubscriptionStatus.Active && 
                subscription.Status != SubscriptionStatus.Trialing)
            {
                subscription.Tier = SubscriptionTier.Free;
            }
        }

        private SubscriptionStatus MapStripeStatus(string stripeStatus)
        {
            return stripeStatus?.ToLowerInvariant() switch
            {
                "active" => SubscriptionStatus.Active,
                "past_due" => SubscriptionStatus.PastDue,
                "unpaid" => SubscriptionStatus.Unpaid,
                "canceled" => SubscriptionStatus.Canceled,
                "incomplete" => SubscriptionStatus.Incomplete,
                "incomplete_expired" => SubscriptionStatus.IncompleteExpired,
                "trialing" => SubscriptionStatus.Trialing,
                "paused" => SubscriptionStatus.Paused,
                _ => SubscriptionStatus.None
            };
        }

        private string GetPriceIdForTier(SubscriptionTier tier)
        {
            return tier switch
            {
                SubscriptionTier.Basic => _config.BasicPriceId,
                SubscriptionTier.Premium => _config.ProPriceId,
                SubscriptionTier.Enterprise => _config.EnterprisePriceId,
                _ => string.Empty
            };
        }

        private SubscriptionTier GetTierFromPriceId(string priceId)
        {
            if (priceId == _config.BasicPriceId)
                return SubscriptionTier.Basic;
            if (priceId == _config.ProPriceId)
                return SubscriptionTier.Premium;
            if (priceId == _config.EnterprisePriceId)
                return SubscriptionTier.Enterprise;

            return SubscriptionTier.Free;
        }

        private async Task<bool> HandleInvoicePaymentSucceededAsync(Event stripeEvent)
        {
            var invoice = stripeEvent.Data.Object as Stripe.Invoice;
            if (invoice == null)
            {
                _logger.LogWarning("Invoice data is null in event {EventId}", stripeEvent.Id);
                return false;
            }

            // Get the subscription ID from the first line item
            var subscriptionId = invoice.Lines?.Data?.FirstOrDefault()?.SubscriptionId 
                ?? invoice.Metadata?.GetValueOrDefault("subscription_id");
            
            // Only process if associated with a subscription
            if (string.IsNullOrWhiteSpace(subscriptionId))
            {
                _logger.LogInformation("Invoice {InvoiceId} not associated with subscription, skipping", invoice.Id);
                return true;
            }

            var subscription = await _subscriptionRepository.GetSubscriptionBySubscriptionIdAsync(subscriptionId);
            if (subscription == null)
            {
                _logger.LogWarning("Subscription not found for invoice {InvoiceId}", invoice.Id);
                return false;
            }

            // Reset payment failure counters on successful payment
            subscription.PaymentFailureCount = 0;
            subscription.LastPaymentFailure = null;
            subscription.LastPaymentFailureReason = null;
            subscription.LastUpdated = DateTime.UtcNow;

            await _subscriptionRepository.SaveSubscriptionAsync(subscription);

            await _subscriptionRepository.MarkEventProcessedAsync(new SubscriptionWebhookEvent
            {
                EventId = stripeEvent.Id,
                EventType = stripeEvent.Type,
                UserAddress = subscription.UserAddress,
                StripeSubscriptionId = subscription.StripeSubscriptionId,
                Tier = subscription.Tier,
                Status = subscription.Status,
                Success = true
            });

            _logger.LogInformation(
                "Processed invoice payment success for user {UserAddress}, invoice {InvoiceId}",
                subscription.UserAddress, invoice.Id);

            return true;
        }

        private async Task<bool> HandleInvoicePaymentFailedAsync(Event stripeEvent)
        {
            var invoice = stripeEvent.Data.Object as Stripe.Invoice;
            if (invoice == null)
            {
                _logger.LogWarning("Invoice data is null in event {EventId}", stripeEvent.Id);
                return false;
            }

            // Get the subscription ID from the first line item
            var subscriptionId = invoice.Lines?.Data?.FirstOrDefault()?.SubscriptionId 
                ?? invoice.Metadata?.GetValueOrDefault("subscription_id");
            
            // Only process if associated with a subscription
            if (string.IsNullOrWhiteSpace(subscriptionId))
            {
                _logger.LogInformation("Invoice {InvoiceId} not associated with subscription, skipping", invoice.Id);
                return true;
            }

            var subscription = await _subscriptionRepository.GetSubscriptionBySubscriptionIdAsync(subscriptionId);
            if (subscription == null)
            {
                _logger.LogWarning("Subscription not found for invoice {InvoiceId}", invoice.Id);
                return false;
            }

            // Track payment failure
            subscription.PaymentFailureCount++;
            subscription.LastPaymentFailure = DateTime.UtcNow;
            subscription.LastPaymentFailureReason = invoice.LastFinalizationError?.Message ?? "Payment failed";
            subscription.LastUpdated = DateTime.UtcNow;

            await _subscriptionRepository.SaveSubscriptionAsync(subscription);

            await _subscriptionRepository.MarkEventProcessedAsync(new SubscriptionWebhookEvent
            {
                EventId = stripeEvent.Id,
                EventType = stripeEvent.Type,
                UserAddress = subscription.UserAddress,
                StripeSubscriptionId = subscription.StripeSubscriptionId,
                Tier = subscription.Tier,
                Status = subscription.Status,
                Success = true,
                ErrorMessage = subscription.LastPaymentFailureReason
            });

            _logger.LogWarning(
                "Processed invoice payment failure for user {UserAddress}, invoice {InvoiceId}, reason: {Reason}",
                subscription.UserAddress, invoice.Id, subscription.LastPaymentFailureReason);

            return true;
        }

        private async Task<bool> HandleDisputeCreatedAsync(Event stripeEvent)
        {
            var dispute = stripeEvent.Data.Object as Stripe.Dispute;
            if (dispute == null)
            {
                _logger.LogWarning("Dispute data is null in event {EventId}", stripeEvent.Id);
                return false;
            }

            // Find subscription by customer ID from the charge
            var chargeService = new Stripe.ChargeService();
            var charge = await chargeService.GetAsync(dispute.ChargeId);
            
            if (charge == null || string.IsNullOrWhiteSpace(charge.CustomerId))
            {
                _logger.LogWarning("Could not find customer for dispute {DisputeId}", dispute.Id);
                return false;
            }

            var subscription = await _subscriptionRepository.GetSubscriptionByCustomerIdAsync(charge.CustomerId);
            if (subscription == null)
            {
                _logger.LogWarning("Subscription not found for customer {CustomerId}", charge.CustomerId);
                return false;
            }

            // Mark dispute in subscription state
            subscription.HasActiveDispute = true;
            subscription.LastDisputeDate = DateTime.UtcNow;
            subscription.LastUpdated = DateTime.UtcNow;

            await _subscriptionRepository.SaveSubscriptionAsync(subscription);

            await _subscriptionRepository.MarkEventProcessedAsync(new SubscriptionWebhookEvent
            {
                EventId = stripeEvent.Id,
                EventType = stripeEvent.Type,
                UserAddress = subscription.UserAddress,
                StripeSubscriptionId = subscription.StripeSubscriptionId,
                Tier = subscription.Tier,
                Status = subscription.Status,
                Success = true,
                ErrorMessage = $"Dispute created: {dispute.Reason}"
            });

            _logger.LogWarning(
                "Processed dispute created for user {UserAddress}, dispute {DisputeId}, reason: {Reason}",
                subscription.UserAddress, dispute.Id, dispute.Reason);

            return true;
        }

        /// <inheritdoc/>
        public async Task<SubscriptionEntitlements> GetEntitlementsAsync(string userAddress)
        {
            var subscription = await GetSubscriptionStatusAsync(userAddress);
            return MapTierToEntitlements(subscription.Tier);
        }

        private SubscriptionEntitlements MapTierToEntitlements(SubscriptionTier tier)
        {
            return tier switch
            {
                SubscriptionTier.Free => new SubscriptionEntitlements
                {
                    Tier = SubscriptionTier.Free,
                    MaxTokenDeployments = 1,
                    MaxWhitelistedAddresses = 10,
                    MaxComplianceReports = 1,
                    AdvancedComplianceEnabled = false,
                    MultiJurisdictionEnabled = false,
                    CustomBrandingEnabled = false,
                    PrioritySupportEnabled = false,
                    ApiAccessEnabled = true,
                    WebhooksEnabled = false,
                    AuditExportsEnabled = false,
                    MaxAuditExports = 0,
                    SlaEnabled = false,
                    SlaUptimePercentage = null
                },
                SubscriptionTier.Basic => new SubscriptionEntitlements
                {
                    Tier = SubscriptionTier.Basic,
                    MaxTokenDeployments = 10,
                    MaxWhitelistedAddresses = 100,
                    MaxComplianceReports = 10,
                    AdvancedComplianceEnabled = true,
                    MultiJurisdictionEnabled = false,
                    CustomBrandingEnabled = false,
                    PrioritySupportEnabled = false,
                    ApiAccessEnabled = true,
                    WebhooksEnabled = true,
                    AuditExportsEnabled = true,
                    MaxAuditExports = 5,
                    SlaEnabled = false,
                    SlaUptimePercentage = null
                },
                SubscriptionTier.Premium => new SubscriptionEntitlements
                {
                    Tier = SubscriptionTier.Premium,
                    MaxTokenDeployments = 100,
                    MaxWhitelistedAddresses = 1000,
                    MaxComplianceReports = 100,
                    AdvancedComplianceEnabled = true,
                    MultiJurisdictionEnabled = true,
                    CustomBrandingEnabled = true,
                    PrioritySupportEnabled = true,
                    ApiAccessEnabled = true,
                    WebhooksEnabled = true,
                    AuditExportsEnabled = true,
                    MaxAuditExports = 50,
                    SlaEnabled = true,
                    SlaUptimePercentage = 99.5
                },
                SubscriptionTier.Enterprise => new SubscriptionEntitlements
                {
                    Tier = SubscriptionTier.Enterprise,
                    MaxTokenDeployments = -1, // Unlimited
                    MaxWhitelistedAddresses = -1, // Unlimited
                    MaxComplianceReports = -1, // Unlimited
                    AdvancedComplianceEnabled = true,
                    MultiJurisdictionEnabled = true,
                    CustomBrandingEnabled = true,
                    PrioritySupportEnabled = true,
                    ApiAccessEnabled = true,
                    WebhooksEnabled = true,
                    AuditExportsEnabled = true,
                    MaxAuditExports = -1, // Unlimited
                    SlaEnabled = true,
                    SlaUptimePercentage = 99.9
                },
                _ => new SubscriptionEntitlements
                {
                    Tier = SubscriptionTier.Free,
                    MaxTokenDeployments = 1,
                    MaxWhitelistedAddresses = 10,
                    MaxComplianceReports = 1,
                    AdvancedComplianceEnabled = false,
                    MultiJurisdictionEnabled = false,
                    CustomBrandingEnabled = false,
                    PrioritySupportEnabled = false,
                    ApiAccessEnabled = true,
                    WebhooksEnabled = false,
                    AuditExportsEnabled = false,
                    MaxAuditExports = 0,
                    SlaEnabled = false,
                    SlaUptimePercentage = null
                }
            };
        }
    }
}

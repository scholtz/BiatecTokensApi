namespace BiatecTokensApi.Configuration
{
    /// <summary>
    /// Configuration for Stripe API integration
    /// </summary>
    public class StripeConfig
    {
        /// <summary>
        /// Stripe secret API key
        /// </summary>
        public string SecretKey { get; set; } = string.Empty;

        /// <summary>
        /// Stripe publishable API key
        /// </summary>
        public string PublishableKey { get; set; } = string.Empty;

        /// <summary>
        /// Webhook secret for validating webhook events
        /// </summary>
        public string WebhookSecret { get; set; } = string.Empty;

        /// <summary>
        /// Stripe price ID for Basic tier subscription
        /// </summary>
        public string BasicPriceId { get; set; } = string.Empty;

        /// <summary>
        /// Stripe price ID for Pro tier subscription
        /// </summary>
        public string ProPriceId { get; set; } = string.Empty;

        /// <summary>
        /// Stripe price ID for Enterprise tier subscription
        /// </summary>
        public string EnterprisePriceId { get; set; } = string.Empty;

        /// <summary>
        /// Success URL for checkout redirect
        /// </summary>
        public string CheckoutSuccessUrl { get; set; } = string.Empty;

        /// <summary>
        /// Cancel URL for checkout redirect
        /// </summary>
        public string CheckoutCancelUrl { get; set; } = string.Empty;
    }
}

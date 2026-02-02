using BiatecTokensApi.Controllers;
using BiatecTokensApi.Models.Subscription;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System.Security.Claims;

namespace BiatecTokensTests
{
    /// <summary>
    /// Tests for Subscription API controller
    /// </summary>
    [TestFixture]
    public class SubscriptionControllerTests
    {
        private Mock<IStripeService> _stripeServiceMock;
        private Mock<ILogger<SubscriptionController>> _loggerMock;
        private SubscriptionController _controller;

        private const string TestUserAddress = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";

        [SetUp]
        public void Setup()
        {
            _stripeServiceMock = new Mock<IStripeService>();
            _loggerMock = new Mock<ILogger<SubscriptionController>>();
            _controller = new SubscriptionController(_stripeServiceMock.Object, _loggerMock.Object);

            // Setup authenticated user
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, TestUserAddress)
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            };
        }

        #region CreateCheckoutSession Tests

        [Test]
        public async Task CreateCheckoutSession_ValidRequest_ReturnsSuccess()
        {
            // Arrange
            var request = new CreateCheckoutSessionRequest
            {
                Tier = SubscriptionTier.Basic
            };

            var expectedResponse = new CreateCheckoutSessionResponse
            {
                Success = true,
                SessionId = "cs_test123",
                CheckoutUrl = "https://checkout.stripe.com/test123"
            };

            _stripeServiceMock.Setup(x => x.CreateCheckoutSessionAsync(TestUserAddress, SubscriptionTier.Basic))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.CreateCheckoutSession(request);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            var response = okResult?.Value as CreateCheckoutSessionResponse;
            Assert.That(response, Is.Not.Null);
            Assert.That(response.Success, Is.True);
            Assert.That(response.SessionId, Is.EqualTo("cs_test123"));
            Assert.That(response.CheckoutUrl, Is.Not.Null);
        }

        [Test]
        public async Task CreateCheckoutSession_ServiceFailure_ReturnsBadRequest()
        {
            // Arrange
            var request = new CreateCheckoutSessionRequest
            {
                Tier = SubscriptionTier.Enterprise
            };

            var expectedResponse = new CreateCheckoutSessionResponse
            {
                Success = false,
                ErrorMessage = "Price ID not configured"
            };

            _stripeServiceMock.Setup(x => x.CreateCheckoutSessionAsync(TestUserAddress, SubscriptionTier.Enterprise))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.CreateCheckoutSession(request);

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
            var badRequestResult = result as BadRequestObjectResult;
            var response = badRequestResult?.Value as CreateCheckoutSessionResponse;
            Assert.That(response, Is.Not.Null);
            Assert.That(response.Success, Is.False);
            Assert.That(response.ErrorMessage, Is.Not.Null);
        }

        [Test]
        public async Task CreateCheckoutSession_NoAuthentication_ReturnsUnauthorized()
        {
            // Arrange
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() }
            };

            var request = new CreateCheckoutSessionRequest
            {
                Tier = SubscriptionTier.Basic
            };

            // Act
            var result = await _controller.CreateCheckoutSession(request);

            // Assert
            Assert.That(result, Is.InstanceOf<UnauthorizedObjectResult>());
        }

        #endregion

        #region CreateBillingPortalSession Tests

        [Test]
        public async Task CreateBillingPortalSession_ValidRequest_ReturnsSuccess()
        {
            // Arrange
            var request = new CreateBillingPortalSessionRequest
            {
                ReturnUrl = "https://example.com/return"
            };

            var expectedResponse = new CreateBillingPortalSessionResponse
            {
                Success = true,
                PortalUrl = "https://billing.stripe.com/session/test123"
            };

            _stripeServiceMock.Setup(x => x.CreateBillingPortalSessionAsync(TestUserAddress, request.ReturnUrl))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.CreateBillingPortalSession(request);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            var response = okResult?.Value as CreateBillingPortalSessionResponse;
            Assert.That(response, Is.Not.Null);
            Assert.That(response.Success, Is.True);
            Assert.That(response.PortalUrl, Is.Not.Null);
        }

        [Test]
        public async Task CreateBillingPortalSession_NoSubscription_ReturnsBadRequest()
        {
            // Arrange
            var request = new CreateBillingPortalSessionRequest();

            var expectedResponse = new CreateBillingPortalSessionResponse
            {
                Success = false,
                ErrorMessage = "No active subscription found"
            };

            _stripeServiceMock.Setup(x => x.CreateBillingPortalSessionAsync(TestUserAddress, null))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.CreateBillingPortalSession(request);

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task CreateBillingPortalSession_NoAuthentication_ReturnsUnauthorized()
        {
            // Arrange
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() }
            };

            // Act
            var result = await _controller.CreateBillingPortalSession(null);

            // Assert
            Assert.That(result, Is.InstanceOf<UnauthorizedObjectResult>());
        }

        #endregion

        #region GetSubscriptionStatus Tests

        [Test]
        public async Task GetSubscriptionStatus_ValidUser_ReturnsStatus()
        {
            // Arrange
            var expectedSubscription = new SubscriptionState
            {
                UserAddress = TestUserAddress,
                Tier = SubscriptionTier.Premium,
                Status = SubscriptionStatus.Active,
                StripeCustomerId = "cus_test123",
                StripeSubscriptionId = "sub_test123"
            };

            _stripeServiceMock.Setup(x => x.GetSubscriptionStatusAsync(TestUserAddress))
                .ReturnsAsync(expectedSubscription);

            // Act
            var result = await _controller.GetSubscriptionStatus();

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            var response = okResult?.Value as SubscriptionStatusResponse;
            Assert.That(response, Is.Not.Null);
            Assert.That(response.Success, Is.True);
            Assert.That(response.Subscription, Is.Not.Null);
            Assert.That(response.Subscription.Tier, Is.EqualTo(SubscriptionTier.Premium));
            Assert.That(response.Subscription.Status, Is.EqualTo(SubscriptionStatus.Active));
        }

        [Test]
        public async Task GetSubscriptionStatus_NewUser_ReturnsFreeTier()
        {
            // Arrange
            var expectedSubscription = new SubscriptionState
            {
                UserAddress = TestUserAddress,
                Tier = SubscriptionTier.Free,
                Status = SubscriptionStatus.None
            };

            _stripeServiceMock.Setup(x => x.GetSubscriptionStatusAsync(TestUserAddress))
                .ReturnsAsync(expectedSubscription);

            // Act
            var result = await _controller.GetSubscriptionStatus();

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            var response = okResult?.Value as SubscriptionStatusResponse;
            Assert.That(response, Is.Not.Null);
            Assert.That(response.Success, Is.True);
            Assert.That(response.Subscription?.Tier, Is.EqualTo(SubscriptionTier.Free));
            Assert.That(response.Subscription?.Status, Is.EqualTo(SubscriptionStatus.None));
        }

        [Test]
        public async Task GetSubscriptionStatus_NoAuthentication_ReturnsUnauthorized()
        {
            // Arrange
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() }
            };

            // Act
            var result = await _controller.GetSubscriptionStatus();

            // Assert
            Assert.That(result, Is.InstanceOf<UnauthorizedObjectResult>());
        }

        #endregion

        #region HandleWebhook Tests

        [Test]
        public async Task HandleWebhook_ValidSignature_ReturnsOk()
        {
            // Arrange
            var webhookJson = "{\"id\":\"evt_test123\",\"type\":\"customer.subscription.created\"}";
            var signature = "test_signature";

            // Setup mock HTTP context with body and header
            var context = new DefaultHttpContext();
            context.Request.Body = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(webhookJson));
            context.Request.Headers["Stripe-Signature"] = signature;
            _controller.ControllerContext = new ControllerContext { HttpContext = context };

            _stripeServiceMock.Setup(x => x.ProcessWebhookEventAsync(webhookJson, signature))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.HandleWebhook();

            // Assert
            Assert.That(result, Is.InstanceOf<OkResult>());
        }

        [Test]
        public async Task HandleWebhook_ProcessingFailed_ReturnsBadRequest()
        {
            // Arrange
            var webhookJson = "{\"id\":\"evt_test123\",\"type\":\"invalid.event\"}";
            var signature = "test_signature";

            var context = new DefaultHttpContext();
            context.Request.Body = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(webhookJson));
            context.Request.Headers["Stripe-Signature"] = signature;
            _controller.ControllerContext = new ControllerContext { HttpContext = context };

            _stripeServiceMock.Setup(x => x.ProcessWebhookEventAsync(webhookJson, signature))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.HandleWebhook();

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task HandleWebhook_MissingSignature_ReturnsBadRequest()
        {
            // Arrange
            var webhookJson = "{\"id\":\"evt_test123\"}";

            var context = new DefaultHttpContext();
            context.Request.Body = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(webhookJson));
            // No signature header
            _controller.ControllerContext = new ControllerContext { HttpContext = context };

            // Act
            var result = await _controller.HandleWebhook();

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        }

        #endregion
    }
}

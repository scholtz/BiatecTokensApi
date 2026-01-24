using BiatecTokensApi.Filters;
using BiatecTokensApi.Models.Whitelist;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;

namespace BiatecTokensTests
{
    [TestFixture]
    public class WhitelistEnforcementTests
    {
        private Mock<IWhitelistService> _whitelistServiceMock;
        private Mock<ILogger<WhitelistEnforcementAttribute>> _loggerMock;
        private ServiceCollection _services;
        private const string TestUserAddress = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";
        private const string TestAddress1 = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";
        private const string TestAddress2 = "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBY5HFKQ";

        [SetUp]
        public void Setup()
        {
            _whitelistServiceMock = new Mock<IWhitelistService>();
            _loggerMock = new Mock<ILogger<WhitelistEnforcementAttribute>>();
            
            _services = new ServiceCollection();
            _services.AddSingleton(_whitelistServiceMock.Object);
            _services.AddSingleton(_loggerMock.Object);
        }

        [Test]
        public async Task OnActionExecutionAsync_WithWhitelistedAddresses_ShouldAllowOperation()
        {
            // Arrange
            var assetId = (ulong)12345;
            var attribute = new WhitelistEnforcementAttribute
            {
                AssetIdParameter = "assetId",
                AddressParameters = new[] { "fromAddress", "toAddress" }
            };

            var context = CreateActionContext(new
            {
                assetId = assetId,
                fromAddress = TestAddress1,
                toAddress = TestAddress2
            });

            // Mock whitelist service to return success for both addresses
            _whitelistServiceMock
                .Setup(s => s.ValidateTransferAsync(It.IsAny<ValidateTransferRequest>(), It.IsAny<string>()))
                .ReturnsAsync(new ValidateTransferResponse
                {
                    Success = true,
                    IsAllowed = true
                });

            var nextCalled = false;
            Task<ActionExecutedContext> Next()
            {
                nextCalled = true;
                return Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), null));
            }

            // Act
            await attribute.OnActionExecutionAsync(context, Next);

            // Assert
            Assert.That(nextCalled, Is.True, "Next action should have been called");
            Assert.That(context.Result, Is.Null, "Result should be null (action allowed to proceed)");
        }

        [Test]
        public async Task OnActionExecutionAsync_WithNonWhitelistedAddress_ShouldBlockOperation()
        {
            // Arrange
            var assetId = (ulong)12345;
            var attribute = new WhitelistEnforcementAttribute
            {
                AssetIdParameter = "assetId",
                AddressParameters = new[] { "fromAddress" }
            };

            var context = CreateActionContext(new
            {
                assetId = assetId,
                fromAddress = TestAddress1
            });

            // Mock whitelist service to return not allowed
            _whitelistServiceMock
                .Setup(s => s.ValidateTransferAsync(It.IsAny<ValidateTransferRequest>(), It.IsAny<string>()))
                .ReturnsAsync(new ValidateTransferResponse
                {
                    Success = true,
                    IsAllowed = false,
                    DenialReason = "Address not whitelisted for this asset"
                });

            var nextCalled = false;
            Task<ActionExecutedContext> Next()
            {
                nextCalled = true;
                return Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), null));
            }

            // Act
            await attribute.OnActionExecutionAsync(context, Next);

            // Assert
            Assert.That(nextCalled, Is.False, "Next action should NOT have been called");
            Assert.That(context.Result, Is.Not.Null, "Result should be set");
            Assert.That(context.Result, Is.InstanceOf<ObjectResult>());
            
            var result = (ObjectResult)context.Result!;
            Assert.That(result.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
        }

        [Test]
        public async Task OnActionExecutionAsync_WithExpiredWhitelist_ShouldBlockOperation()
        {
            // Arrange
            var assetId = (ulong)12345;
            var attribute = new WhitelistEnforcementAttribute
            {
                AssetIdParameter = "assetId",
                AddressParameters = new[] { "address" }
            };

            var context = CreateActionContext(new
            {
                assetId = assetId,
                address = TestAddress1
            });

            // Mock whitelist service to return expired status
            _whitelistServiceMock
                .Setup(s => s.ValidateTransferAsync(It.IsAny<ValidateTransferRequest>(), It.IsAny<string>()))
                .ReturnsAsync(new ValidateTransferResponse
                {
                    Success = true,
                    IsAllowed = false,
                    DenialReason = "Whitelist entry expired on 2024-01-01"
                });

            var nextCalled = false;
            Task<ActionExecutedContext> Next()
            {
                nextCalled = true;
                return Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), null));
            }

            // Act
            await attribute.OnActionExecutionAsync(context, Next);

            // Assert
            Assert.That(nextCalled, Is.False, "Next action should NOT have been called");
            Assert.That(context.Result, Is.Not.Null);
            Assert.That(context.Result, Is.InstanceOf<ObjectResult>());
            
            var result = (ObjectResult)context.Result!;
            Assert.That(result.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
        }

        [Test]
        public async Task OnActionExecutionAsync_WithInvalidAssetId_ShouldReturnBadRequest()
        {
            // Arrange
            var attribute = new WhitelistEnforcementAttribute
            {
                AssetIdParameter = "assetId",
                AddressParameters = new[] { "address" }
            };

            var context = CreateActionContext(new
            {
                // No assetId parameter
                address = TestAddress1
            });

            var nextCalled = false;
            Task<ActionExecutedContext> Next()
            {
                nextCalled = true;
                return Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), null));
            }

            // Act
            await attribute.OnActionExecutionAsync(context, Next);

            // Assert
            Assert.That(nextCalled, Is.False);
            Assert.That(context.Result, Is.InstanceOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task OnActionExecutionAsync_WithMultipleAddresses_OneNotWhitelisted_ShouldBlockOperation()
        {
            // Arrange
            var assetId = (ulong)12345;
            var attribute = new WhitelistEnforcementAttribute
            {
                AssetIdParameter = "assetId",
                AddressParameters = new[] { "fromAddress", "toAddress" }
            };

            var context = CreateActionContext(new
            {
                assetId = assetId,
                fromAddress = TestAddress1,
                toAddress = TestAddress2
            });

            // Mock whitelist service: first address allowed, second blocked
            var callCount = 0;
            _whitelistServiceMock
                .Setup(s => s.ValidateTransferAsync(It.IsAny<ValidateTransferRequest>(), It.IsAny<string>()))
                .ReturnsAsync(() =>
                {
                    callCount++;
                    return callCount == 1
                        ? new ValidateTransferResponse { Success = true, IsAllowed = true }
                        : new ValidateTransferResponse { Success = true, IsAllowed = false, DenialReason = "Address2 not whitelisted" };
                });

            var nextCalled = false;
            Task<ActionExecutedContext> Next()
            {
                nextCalled = true;
                return Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), null));
            }

            // Act
            await attribute.OnActionExecutionAsync(context, Next);

            // Assert
            Assert.That(nextCalled, Is.False, "Next action should NOT have been called");
            Assert.That(context.Result, Is.InstanceOf<ObjectResult>());
            
            var result = (ObjectResult)context.Result!;
            Assert.That(result.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
        }

        [Test]
        public async Task OnActionExecutionAsync_ValidateUserAddress_ShouldValidateAuthenticatedUser()
        {
            // Arrange
            var assetId = (ulong)12345;
            var attribute = new WhitelistEnforcementAttribute
            {
                AssetIdParameter = "assetId",
                AddressParameters = Array.Empty<string>(),
                ValidateUserAddress = true
            };

            var context = CreateActionContext(new
            {
                assetId = assetId
            });

            // Mock whitelist service to check user address
            string? validatedAddress = null;
            _whitelistServiceMock
                .Setup(s => s.ValidateTransferAsync(It.IsAny<ValidateTransferRequest>(), It.IsAny<string>()))
                .Callback<ValidateTransferRequest, string>((req, _) => validatedAddress = req.FromAddress)
                .ReturnsAsync(new ValidateTransferResponse { Success = true, IsAllowed = true });

            var nextCalled = false;
            Task<ActionExecutedContext> Next()
            {
                nextCalled = true;
                return Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), null));
            }

            // Act
            await attribute.OnActionExecutionAsync(context, Next);

            // Assert
            Assert.That(nextCalled, Is.True);
            Assert.That(validatedAddress, Is.EqualTo(TestUserAddress));
        }

        [Test]
        public async Task OnActionExecutionAsync_WithNoAuthentication_ShouldReturnUnauthorized()
        {
            // Arrange
            var assetId = (ulong)12345;
            var attribute = new WhitelistEnforcementAttribute
            {
                AssetIdParameter = "assetId",
                AddressParameters = new[] { "address" }
            };

            var context = CreateActionContext(new
            {
                assetId = assetId,
                address = TestAddress1
            }, includeUser: false);

            var nextCalled = false;
            Task<ActionExecutedContext> Next()
            {
                nextCalled = true;
                return Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), null));
            }

            // Act
            await attribute.OnActionExecutionAsync(context, Next);

            // Assert
            Assert.That(nextCalled, Is.False);
            Assert.That(context.Result, Is.InstanceOf<UnauthorizedObjectResult>());
        }

        [Test]
        public async Task OnActionExecutionAsync_WithServiceException_ShouldReturnInternalServerError()
        {
            // Arrange
            var assetId = (ulong)12345;
            var attribute = new WhitelistEnforcementAttribute
            {
                AssetIdParameter = "assetId",
                AddressParameters = new[] { "address" }
            };

            var context = CreateActionContext(new
            {
                assetId = assetId,
                address = TestAddress1
            });

            // Mock whitelist service to throw exception
            _whitelistServiceMock
                .Setup(s => s.ValidateTransferAsync(It.IsAny<ValidateTransferRequest>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("Database connection failed"));

            var nextCalled = false;
            Task<ActionExecutedContext> Next()
            {
                nextCalled = true;
                return Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), null));
            }

            // Act
            await attribute.OnActionExecutionAsync(context, Next);

            // Assert
            Assert.That(nextCalled, Is.False);
            Assert.That(context.Result, Is.InstanceOf<ObjectResult>());
            
            var result = (ObjectResult)context.Result!;
            Assert.That(result.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
        }

        [Test]
        public async Task OnActionExecutionAsync_WithPropertyBasedParameters_ShouldExtractFromRequestObject()
        {
            // Arrange
            var assetId = (ulong)12345;
            var attribute = new WhitelistEnforcementAttribute
            {
                AssetIdParameter = "AssetId", // Property name
                AddressParameters = new[] { "FromAddress", "ToAddress" }
            };

            var request = new TestTransferRequest
            {
                AssetId = assetId,
                FromAddress = TestAddress1,
                ToAddress = TestAddress2
            };

            var context = CreateActionContext(new { request = request });

            _whitelistServiceMock
                .Setup(s => s.ValidateTransferAsync(It.IsAny<ValidateTransferRequest>(), It.IsAny<string>()))
                .ReturnsAsync(new ValidateTransferResponse { Success = true, IsAllowed = true });

            var nextCalled = false;
            Task<ActionExecutedContext> Next()
            {
                nextCalled = true;
                return Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), null));
            }

            // Act
            await attribute.OnActionExecutionAsync(context, Next);

            // Assert
            Assert.That(nextCalled, Is.True, "Should extract parameters from nested object properties");
        }

        /// <summary>
        /// Helper method to create an ActionExecutingContext for testing
        /// </summary>
        private ActionExecutingContext CreateActionContext(object arguments, bool includeUser = true)
        {
            var serviceProvider = _services.BuildServiceProvider();
            var httpContext = new DefaultHttpContext
            {
                RequestServices = serviceProvider
            };

            if (includeUser)
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, TestUserAddress)
                };
                httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
            }

            var actionContext = new ActionContext(
                httpContext,
                new RouteData(),
                new ActionDescriptor()
            );

            var actionArguments = new Dictionary<string, object?>();
            if (arguments != null)
            {
                foreach (var prop in arguments.GetType().GetProperties())
                {
                    actionArguments[prop.Name] = prop.GetValue(arguments);
                }
            }

            return new ActionExecutingContext(
                actionContext,
                new List<IFilterMetadata>(),
                actionArguments,
                controller: null
            );
        }

        /// <summary>
        /// Test request class for property-based parameter extraction testing
        /// </summary>
        private class TestTransferRequest
        {
            public ulong AssetId { get; set; }
            public string FromAddress { get; set; } = string.Empty;
            public string ToAddress { get; set; } = string.Empty;
        }
    }
}

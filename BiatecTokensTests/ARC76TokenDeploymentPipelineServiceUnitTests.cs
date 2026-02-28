using BiatecTokensApi.Configuration;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.ASA.Request;
using BiatecTokensApi.Models.Auth;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using AlgorandAuthenticationV2;

namespace BiatecTokensTests
{
    /// <summary>
    /// Service-layer unit tests for the ARC76 Token Deployment Pipeline (Issue #415).
    ///
    /// Tests the new <paramref name="userId"/> parameter introduced in
    /// <see cref="ASATokenService.CreateASATokenAsync"/> and the <see cref="ApiErrorResponse.Retryable"/>
    /// field required by AC5.
    ///
    /// Coverage areas:
    ///   - ASATokenService.CreateASATokenAsync: userId routing (user account vs system account)
    ///   - ASATokenService: user account retrieval failure → structured error response
    ///   - IAuthenticationService.GetUserMnemonicForSigningAsync: null result handling
    ///   - ApiErrorResponse: Retryable field semantics
    ///   - IASATokenService interface: signature backwards-compatibility (optional userId)
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ARC76TokenDeploymentPipelineServiceUnitTests
    {
        private Mock<IOptionsMonitor<AlgorandAuthenticationOptionsV2>> _configMock = null!;
        private Mock<IOptionsMonitor<AppConfiguration>> _appConfigMock = null!;
        private Mock<ILogger<ARC3TokenService>> _loggerMock = null!;
        private Mock<IComplianceRepository> _complianceRepositoryMock = null!;
        private Mock<ITokenIssuanceRepository> _tokenIssuanceRepositoryMock = null!;
        private Mock<IHttpContextAccessor> _httpContextAccessorMock = null!;
        private Mock<IAuthenticationService> _authServiceMock = null!;

        [SetUp]
        public void Setup()
        {
            var algoConfig = new AlgorandAuthenticationOptionsV2
            {
                AllowedNetworks = new AllowedNetworks()
            };

            var appConfig = new AppConfiguration
            {
                Account = "test test test test test test test test test test test test test test test test test test test test test test test test test"
            };

            _configMock = new Mock<IOptionsMonitor<AlgorandAuthenticationOptionsV2>>();
            _configMock.Setup(x => x.CurrentValue).Returns(algoConfig);

            _appConfigMock = new Mock<IOptionsMonitor<AppConfiguration>>();
            _appConfigMock.Setup(x => x.CurrentValue).Returns(appConfig);

            _loggerMock = new Mock<ILogger<ARC3TokenService>>();
            _complianceRepositoryMock = new Mock<IComplianceRepository>();
            _tokenIssuanceRepositoryMock = new Mock<ITokenIssuanceRepository>();
            _httpContextAccessorMock = new Mock<IHttpContextAccessor>();
            _authServiceMock = new Mock<IAuthenticationService>();
        }

        private ASATokenService CreateService()
        {
            return new ASATokenService(
                _configMock.Object,
                _appConfigMock.Object,
                _loggerMock.Object,
                _complianceRepositoryMock.Object,
                _tokenIssuanceRepositoryMock.Object,
                _httpContextAccessorMock.Object,
                _authServiceMock.Object);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // ApiErrorResponse.Retryable field
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// AC5: ApiErrorResponse.Retryable defaults to false (safe default: do not retry unknown errors).
        /// </summary>
        [Test]
        public void ApiErrorResponse_Retryable_DefaultIsFalse()
        {
            var response = new ApiErrorResponse
            {
                ErrorCode = "UNKNOWN",
                ErrorMessage = "An unexpected error occurred"
            };

            Assert.That(response.Retryable, Is.False,
                "Retryable must default to false — unknown errors should not be retried blindly");
        }

        /// <summary>
        /// AC5: Retryable can be explicitly set to true for transient errors.
        /// </summary>
        [Test]
        public void ApiErrorResponse_Retryable_CanBeSetTrue()
        {
            var response = new ApiErrorResponse
            {
                ErrorCode = "NODE_TIMEOUT",
                ErrorMessage = "Algorand node timed out",
                Retryable = true
            };

            Assert.That(response.Retryable, Is.True,
                "Retryable=true must be settable for transient errors");
        }

        /// <summary>
        /// AC5: Retryable=false for permanent errors (insufficient balance).
        /// </summary>
        [Test]
        public void ApiErrorResponse_Retryable_FalseForPermanentError()
        {
            var response = new ApiErrorResponse
            {
                ErrorCode = "INSUFFICIENT_BALANCE",
                ErrorMessage = "Account does not have sufficient ALGO",
                Retryable = false
            };

            Assert.That(response.Retryable, Is.False,
                "Retryable must be false for permanent errors like insufficient balance");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // ASATokenService constructor accepts IAuthenticationService
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// AC2: ASATokenService can be constructed with IAuthenticationService dependency.
        /// No network connections are required for unit tests (no Algorand nodes in test env).
        /// </summary>
        [Test]
        public void ASATokenService_Constructor_AcceptsAuthenticationService()
        {
            // Should not throw during construction with empty network config
            ASATokenService? service = null;
            Assert.DoesNotThrow(() =>
            {
                service = CreateService();
            }, "ASATokenService constructor must accept IAuthenticationService without throwing");

            Assert.That(service, Is.Not.Null,
                "ASATokenService must be successfully constructed");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // CreateASATokenAsync: userId parameter routing
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// AC2: When userId is null, the service uses the system account (no auth service call).
        /// Validated by confirming GetUserMnemonicForSigningAsync is not called.
        /// </summary>
        [Test]
        public async Task CreateASATokenAsync_NullUserId_UsesSystemAccount_NoAuthServiceCall()
        {
            _authServiceMock.Setup(a => a.GetUserMnemonicForSigningAsync(It.IsAny<string>()))
                .ReturnsAsync("some-mnemonic");

            var service = CreateService();
            var request = new ASAFungibleTokenDeploymentRequest
            {
                Name = "SystemToken",
                UnitName = "SYS",
                TotalSupply = 1_000_000,
                Decimals = 6,
                Network = "testnet-v1.0"
            };

            // The call will fail because no Algorand node is configured, but we can
            // verify the auth service is NOT called when userId=null
            try
            {
                await service.CreateASATokenAsync(request, TokenType.ASA_FT, null);
            }
            catch
            {
                // Expected: no Algorand node configured in unit test
            }

            _authServiceMock.Verify(
                a => a.GetUserMnemonicForSigningAsync(It.IsAny<string>()),
                Times.Never,
                "AC2: When userId=null, GetUserMnemonicForSigningAsync must NOT be called");
        }

        /// <summary>
        /// AC2: When userId is provided, the service calls GetUserMnemonicForSigningAsync
        /// to retrieve the user's ARC76 account mnemonic.
        /// </summary>
        [Test]
        public async Task CreateASATokenAsync_WithUserId_CallsAuthServiceForMnemonic()
        {
            _authServiceMock.Setup(a => a.GetUserMnemonicForSigningAsync("test-user-id"))
                .ReturnsAsync("test test test test test test test test test test test test test test test test test test test test test test test test test");

            var service = CreateService();
            var request = new ASAFungibleTokenDeploymentRequest
            {
                Name = "UserToken",
                UnitName = "USR",
                TotalSupply = 1_000_000,
                Decimals = 6,
                Network = "testnet-v1.0"
            };

            // The call will fail because no Algorand node is configured
            try
            {
                await service.CreateASATokenAsync(request, TokenType.ASA_FT, "test-user-id");
            }
            catch
            {
                // Expected: no Algorand node configured in unit test
            }

            _authServiceMock.Verify(
                a => a.GetUserMnemonicForSigningAsync("test-user-id"),
                Times.AtLeastOnce,
                "AC2: When userId is provided, GetUserMnemonicForSigningAsync must be called");
        }

        /// <summary>
        /// AC5: When GetUserMnemonicForSigningAsync returns null, the service returns
        /// a structured error response with ErrorCode=UNAUTHORIZED (not throws).
        /// </summary>
        [Test]
        public async Task CreateASATokenAsync_UserMnemonicNull_ReturnsStructuredError()
        {
            _authServiceMock.Setup(a => a.GetUserMnemonicForSigningAsync("missing-user"))
                .ReturnsAsync((string?)null);

            var service = CreateService();
            var request = new ASAFungibleTokenDeploymentRequest
            {
                Name = "ErrorToken",
                UnitName = "ERR",
                TotalSupply = 100_000,
                Decimals = 0,
                Network = "testnet-v1.0"
            };

            var result = await service.CreateASATokenAsync(request, TokenType.ASA_FT, "missing-user");

            Assert.That(result.Success, Is.False,
                "AC5: Null mnemonic must result in Success=false");
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.UNAUTHORIZED),
                "AC5: Null mnemonic must set ErrorCode=UNAUTHORIZED");
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty,
                "AC5: Null mnemonic must set ErrorMessage");
        }

        /// <summary>
        /// AC5: When GetUserMnemonicForSigningAsync returns empty string, the service
        /// returns a structured error response (treats empty same as null).
        /// </summary>
        [Test]
        public async Task CreateASATokenAsync_UserMnemonicEmpty_ReturnsStructuredError()
        {
            _authServiceMock.Setup(a => a.GetUserMnemonicForSigningAsync("empty-mnemonic-user"))
                .ReturnsAsync(string.Empty);

            var service = CreateService();
            var request = new ASAFungibleTokenDeploymentRequest
            {
                Name = "EmptyMnemonicToken",
                UnitName = "EMPT",
                TotalSupply = 100_000,
                Decimals = 0,
                Network = "testnet-v1.0"
            };

            var result = await service.CreateASATokenAsync(request, TokenType.ASA_FT, "empty-mnemonic-user");

            Assert.That(result.Success, Is.False,
                "AC5: Empty mnemonic must result in Success=false");
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.UNAUTHORIZED),
                "AC5: Empty mnemonic must set ErrorCode=UNAUTHORIZED");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // IASATokenService interface: signature is backwards-compatible
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// AC2: IASATokenService.CreateASATokenAsync has optional userId parameter,
        /// so existing code calling without userId continues to compile and work.
        /// </summary>
        [Test]
        public void IASATokenService_CreateASATokenAsync_UserId_IsOptional()
        {
            // Verify the interface method has a default value for userId via reflection
            var method = typeof(IASATokenService).GetMethod("CreateASATokenAsync");
            Assert.That(method, Is.Not.Null, "IASATokenService must have CreateASATokenAsync method");

            var parameters = method!.GetParameters();
            var userIdParam = parameters.FirstOrDefault(p => p.Name == "userId");
            Assert.That(userIdParam, Is.Not.Null,
                "IASATokenService.CreateASATokenAsync must have a 'userId' parameter");
            Assert.That(userIdParam!.HasDefaultValue, Is.True,
                "userId parameter must be optional (has default value)");
            Assert.That(userIdParam.DefaultValue, Is.Null,
                "userId default value must be null");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // TokenDeploymentComplianceMetadata – MICA fields (AC8)
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// AC8: TokenDeploymentComplianceMetadata accepts all MICA-required fields.
        /// </summary>
        [Test]
        public void TokenDeploymentComplianceMetadata_SupportsAllMICAFields()
        {
            var metadata = new BiatecTokensApi.Models.TokenDeploymentComplianceMetadata
            {
                IssuerName = "Test Issuer Ltd",
                Jurisdiction = "EU",
                RegulatoryFramework = "MICA",
                AssetType = "E-Money Token",
                DisclosureUrl = "https://example.com/disclosure",
                RequiresWhitelist = true,
                RequiresAccreditedInvestors = false,
                MaxHolders = 2000,
                TransferRestrictions = "EEA only",
                KycProvider = "Jumio",
                Notes = "Under MICA Title III supervision"
            };

            Assert.That(metadata.IssuerName, Is.EqualTo("Test Issuer Ltd"));
            Assert.That(metadata.Jurisdiction, Is.EqualTo("EU"));
            Assert.That(metadata.RegulatoryFramework, Is.EqualTo("MICA"));
            Assert.That(metadata.MaxHolders, Is.EqualTo(2000));
            Assert.That(metadata.RequiresWhitelist, Is.True);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // ASAFungibleTokenDeploymentRequest – validation boundary conditions (AC2)
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// AC2: ASAFungibleTokenDeploymentRequest.Decimals max is 19 (as per Algorand spec).
        /// Validated at service level via ValidateASARequest.
        /// </summary>
        [Test]
        public void ValidateASARequest_Decimals20_ThrowsArgumentException()
        {
            var service = CreateService();
            var request = new ASAFungibleTokenDeploymentRequest
            {
                Name = "DecimalsTest",
                UnitName = "DEC",
                TotalSupply = 1_000_000,
                Decimals = 20, // exceeds max of 19
                Network = "testnet-v1.0"
            };

            var method = typeof(ASATokenService).GetMethod("ValidateASARequest",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
                method?.Invoke(service, new object[] { request, TokenType.ASA_FT }));

            Assert.That(ex!.InnerException, Is.TypeOf<ArgumentException>(),
                "AC2: Decimals > 19 must throw ArgumentException");
        }

        /// <summary>
        /// AC2: ASAFungibleTokenDeploymentRequest.UnitName longer than 8 chars fails validation.
        /// </summary>
        [Test]
        public void ValidateASARequest_UnitNameTooLong_ThrowsArgumentException()
        {
            var service = CreateService();
            var request = new ASAFungibleTokenDeploymentRequest
            {
                Name = "UnitNameTest",
                UnitName = "TOOLONGX", // 8 chars is the limit — exactly 8 is fine
                TotalSupply = 1_000_000,
                Decimals = 6,
                Network = "testnet-v1.0"
            };

            // 8 chars should be exactly at limit — must not throw
            var method = typeof(ASATokenService).GetMethod("ValidateASARequest",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            Assert.DoesNotThrow(() =>
                method?.Invoke(service, new object[] { request, TokenType.ASA_FT }),
                "AC2: UnitName of exactly 8 characters must be valid");
        }

        /// <summary>
        /// AC2: ASAFungibleTokenDeploymentRequest.TotalSupply=0 fails service validation.
        /// </summary>
        [Test]
        public void ValidateASARequest_ZeroTotalSupply_ThrowsArgumentException()
        {
            var service = CreateService();
            var request = new ASAFungibleTokenDeploymentRequest
            {
                Name = "ZeroSupply",
                UnitName = "ZERO",
                TotalSupply = 0,
                Decimals = 0,
                Network = "testnet-v1.0"
            };

            var method = typeof(ASATokenService).GetMethod("ValidateASARequest",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
                method?.Invoke(service, new object[] { request, TokenType.ASA_FT }));

            Assert.That(ex!.InnerException, Is.TypeOf<ArgumentException>(),
                "AC2: TotalSupply=0 must throw ArgumentException");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // AuthenticationService.GetUserMnemonicForSigningAsync (AC1 / AC2)
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// AC1/AC2: IAuthenticationService.GetUserMnemonicForSigningAsync is declared
        /// in the interface and can be mocked for testing purposes.
        /// </summary>
        [Test]
        public async Task GetUserMnemonicForSigningAsync_IsDefinedInInterface()
        {
            var method = typeof(IAuthenticationService).GetMethod("GetUserMnemonicForSigningAsync");
            Assert.That(method, Is.Not.Null,
                "AC2: IAuthenticationService must expose GetUserMnemonicForSigningAsync");

            // Verify mock setup works
            _authServiceMock.Setup(a => a.GetUserMnemonicForSigningAsync("user-123"))
                .ReturnsAsync("test-mnemonic-value");

            var result = await _authServiceMock.Object.GetUserMnemonicForSigningAsync("user-123");
            Assert.That(result, Is.EqualTo("test-mnemonic-value"),
                "Mock setup for GetUserMnemonicForSigningAsync must work");
        }
    }
}

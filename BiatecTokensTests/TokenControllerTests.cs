using BiatecTokensApi.Controllers;
using BiatecTokensApi.Models.ERC20.Request;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace BiatecTokensTests
{
    [TestFixture]
    public class TokenControllerTests
    {
        private Mock<IERC20TokenService> _tokenServiceMock;
        private Mock<IARC3TokenService> _arc3TokenServiceMock;
        private Mock<IASATokenService> _asaTokenServiceMock;
        private Mock<IARC200TokenService> _arc200TokenServiceMock;
        private Mock<ILogger<TokenController>> _loggerMock;
        private TokenController _controller;
        private ERC20MintableTokenDeploymentRequest _validDeploymentRequest;

        [SetUp]
        public void Setup()
        {
            _tokenServiceMock = new Mock<IERC20TokenService>();
            _arc3TokenServiceMock = new Mock<IARC3TokenService>();
            _asaTokenServiceMock = new Mock<IASATokenService>();
            _arc200TokenServiceMock = new Mock<IARC200TokenService>();
            _loggerMock = new Mock<ILogger<TokenController>>();
            _controller = new TokenController(_tokenServiceMock.Object, _arc3TokenServiceMock.Object, _asaTokenServiceMock.Object, _arc200TokenServiceMock.Object, _loggerMock.Object);

            // Set up a valid token deployment request for testing
            _validDeploymentRequest = new ERC20MintableTokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "TEST",
                InitialSupply = 1000000,
                Decimals = 18,
                ChainId = 31337, // Required property
                Cap = 10000000, // Required for mintable tokens
                InitialSupplyReceiver = "0x742d35Cc6634C0532925a3b8D162000dDba02C79" // Sample address
            };

            // Set up controller for testing validation
            _controller.ModelState.Clear();
        }
    }
}
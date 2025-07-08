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
        private Mock<IARC3TokenService> _arc3TokenServiceMock; // Added mock for IARC3FungibleTokenService
        private Mock<ILogger<TokenController>> _loggerMock;
        private TokenController _controller;
        private ERC20TokenDeploymentRequest _validDeploymentRequest;

        [SetUp]
        public void Setup()
        {
            _tokenServiceMock = new Mock<IERC20TokenService>();
            _arc3TokenServiceMock = new Mock<IARC3TokenService>(); // Initialize the mock
            _loggerMock = new Mock<ILogger<TokenController>>();
            _controller = new TokenController(_tokenServiceMock.Object, _arc3TokenServiceMock.Object, _loggerMock.Object); // Pass the required argument

            // Set up a valid token deployment request for testing
            _validDeploymentRequest = new ERC20TokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "TEST",
                InitialSupply = 1000000,
                Decimals = 18,
                InitialSupplyReceiver = null, // Will default to deployer
                DeployerPrivateKey = "0xabc123def456abc123def456abc123def456abc123def456abc123def456abcd"
            };

            // Set up controller for testing validation
            _controller.ModelState.Clear();
        }
    }
}
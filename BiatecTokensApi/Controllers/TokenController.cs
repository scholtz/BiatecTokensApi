using Microsoft.AspNetCore.Mvc;
using BiatecTokensApi.Models;
using BiatecTokensApi.Services;

namespace BiatecTokensApi.Controllers
{
    [ApiController]
    [Route("api/v1/token")]
    public class TokenController : ControllerBase
    {
        private readonly ITokenService _tokenService;
        private readonly ILogger<TokenController> _logger;

        public TokenController(ITokenService tokenService, ILogger<TokenController> logger)
        {
            _tokenService = tokenService;
            _logger = logger;
        }

        /// <summary>
        /// Deploys a new BiatecToken on the Base blockchain.
        /// BiatecToken is an advanced ERC20 token with additional features:
        /// - Minting capabilities (owner and authorized minters)
        /// - Burning capabilities (burn and burnFrom)
        /// - Pausable functionality (owner can pause/unpause transfers)
        /// - Ownable (ownership transfer functionality)
        /// The deployer automatically becomes the owner and first minter.
        /// The initial token supply can be sent to a specified address or the deployer.
        /// </summary>
        /// <param name="request">Token deployment parameters including optional initial supply receiver</param>
        /// <returns>Deployment result with contract address and initial supply receiver</returns>
        [HttpPost("deploy")]
        [ProducesResponseType(typeof(TokenDeploymentResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeployToken([FromBody] TokenDeploymentRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var result = await _tokenService.DeployTokenAsync(request);

                if (result.Success)
                {
                    _logger.LogInformation("BiatecToken deployed successfully at address {Address} with transaction {TxHash}", 
                        result.ContractAddress, result.TransactionHash);
                    return Ok(result);
                }
                else
                {
                    _logger.LogError("BiatecToken deployment failed: {Error}", result.ErrorMessage);
                    return StatusCode(StatusCodes.Status500InternalServerError, result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deploying BiatecToken");
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
            }
        }
    }
}

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
        /// Deploys a new ERC20 token on the Base blockchain
        /// </summary>
        /// <param name="request">Token deployment parameters</param>
        /// <returns>Deployment result with contract address</returns>
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
                    _logger.LogInformation("Token deployed successfully at address {Address}", result.ContractAddress);
                    return Ok(result);
                }
                else
                {
                    _logger.LogError("Token deployment failed: {Error}", result.ErrorMessage);
                    return StatusCode(StatusCodes.Status500InternalServerError, result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deploying token");
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
            }
        }
    }
}

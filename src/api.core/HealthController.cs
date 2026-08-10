using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace api.controllers;

[ApiController]
[AllowAnonymous]
[Route("api")]
public class HealthController(ILogger<HealthController> _logger) : ControllerBase
{
    [HttpGet("health"), AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(string))]
    public async Task<ActionResult<string>> Health()
    {
        _logger.LogInformation("health check made");
        return Ok("Just good folks.");
    }
}
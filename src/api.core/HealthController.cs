using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace api;

[ApiController]
[AllowAnonymous]
[Route("api")]
[ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
public class HealthController(ILogger<HealthController> _logger)
{
    [HttpGet("health"), AllowAnonymous]
    public async Task<Ok<string>> Health()
    {
        _logger.LogInformation("health check made");
        return TypedResults.Ok("Just good folks.");
    }
}
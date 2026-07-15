using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace api
{
    [ApiController]
    [AllowAnonymous]
    [Route("api")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    public class HealthController
    {
        [HttpGet("health"), AllowAnonymous]
        public async Task<Ok<string>> Health()
        {
            return TypedResults.Ok("Just good folks.");
        }
    }
}
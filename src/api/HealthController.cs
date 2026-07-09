using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace api
{
    [ApiController]
    [AllowAnonymous]
    [Route("api")]
    public class HealthController
    {
        [HttpGet("health"), AllowAnonymous]
        public async Task<IResult> Health()
        {
            return Results.Ok("Just good folks.");
        }
    }
}
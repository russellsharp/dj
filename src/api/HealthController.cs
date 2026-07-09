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
    public class HealthController
    {
        [HttpGet("health")]
        public async Task<IResult> Health()
        {
            return Results.Ok("Just good folks.");
        }
    }
}
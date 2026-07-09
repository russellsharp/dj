using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using shared.http;

namespace api
{
    [ApiController]
    [Route("token")]
    public class TokenController(ITokenGenerator _tokenGen)
    {
        [HttpGet("anonymous")]
        public async Task<string> RequestAnonymousToken()
        {
            return await _tokenGen.GenerateAnonymousToken();
        }
    }
}
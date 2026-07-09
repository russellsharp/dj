using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using shared.http;

namespace api
{
    [ApiController]
    [AllowAnonymous]
    [Route("api/token")]
    public class TokenController(ITokenGenerator _tokenGen)
    {
        [HttpGet("anonymous"), AllowAnonymous]
        public async Task<string> RequestAnonymousToken()
        {
            return await _tokenGen.GenerateAnonymousToken();
        }
    }
}
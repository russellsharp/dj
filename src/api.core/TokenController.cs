using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;
using shared.http.security;
using Microsoft.Extensions.Logging;

namespace api
{
    [ApiController]
    [AllowAnonymous]
    [Route("api/token")]
    public class TokenController(ITokenGenerator _tokenGen, TestUserDbContext _userDb, ILogger<TokenController> _logger) : ControllerBase
    {
        [HttpGet("anonymous"), AllowAnonymous]
        public async Task<ActionResult<string>> RequestAnonymousToken()
        {
            return Ok(await _tokenGen.GenerateAnonymousToken());
        }

        [HttpPost("scoped"), AllowAnonymous]
        // [IgnoreAntiforgeryToken]
        public async Task<ActionResult<SignInHttpResult>> ExchangeToken()
        {
            var request = HttpContext.GetOpenIddictServerRequest()
                ?? throw new InvalidOperationException("The OAuth request cannot be retrieved.");

            if (request is null)
                return BadRequest();

            if (request.IsClientCredentialsGrantType())
            {
                if (request.ClientId is null || request.ClientSecret is null)
                    return Forbid(authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);

                var user = _userDb.UserInfo.FirstOrDefault(u => u.client_id == request.ClientId);

                if (user == null)
                    return Forbid(authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);

                // 2. Create an identity for the token
                var identity = new ClaimsIdentity(TokenValidationParameters.DefaultAuthenticationType);
                identity.AddClaim(Claims.Subject, request.ClientId);

                var applicationScopes = _userDb.ApplicationScopes.ToList().Select(s => s.Value.ToOidc());

                // 3. Grant only the requested and permitted scopes
                var grantedScopes = request.Scope?
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Where(applicationScopes.Contains)
                    .ToList() ?? new List<string>();

                if (grantedScopes.Count == 0)
                {
                    return BadRequest(new OpenIddictResponse
                    {
                        Error = Errors.InvalidScope,
                        ErrorDescription = "The requested scope is not permitted."
                    });
                }

                // 4. Attach permissions to the final principal destination
                var principal = new ClaimsPrincipal(identity);
                principal.SetScopes(grantedScopes);

                // OpenIddict takes this principal and signs it into a secure JWT
                return SignIn(principal, authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            return BadRequest(new OpenIddictResponse
            {
                Error = Errors.UnsupportedGrantType,
                ErrorDescription = "The specified grant type is not supported."
            });
        }
    }
}
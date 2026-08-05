using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using OpenIdConnectRequest = Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectMessage;
using shared.http;
using shared.util;
using static OpenIddict.Abstractions.OpenIddictConstants;
using shared.http.security;
using Microsoft.OpenApi;
using Microsoft.Extensions.Logging;

namespace api
{
    [ApiController]
    [AllowAnonymous]
    [Route("api/token")]
    public class TokenController(ITokenGenerator _tokenGen, UserDbContext _userDb, ILogger<TokenController> _logger) : Controller
    {
        [HttpGet("anonymous"), AllowAnonymous]
        public async Task<string> RequestAnonymousToken()
        {
            return await _tokenGen.GenerateAnonymousToken();
        }

        [HttpPost("scoped"), AllowAnonymous]
        // [IgnoreAntiforgeryToken]
        public async Task<Results<SignInHttpResult, ForbidHttpResult, BadRequest<OpenIddictResponse>>> ExchangeToken()
        {
            var request = HttpContext.GetOpenIddictServerRequest()
                ?? throw new InvalidOperationException("The OAuth request cannot be retrieved.");

            var registeredClients = _userDb.UserInfo.Select(x => x.client_id).ToList();

            if (request.IsClientCredentialsGrantType())
            {
                if (request.ClientId != null && !registeredClients.Contains(request.ClientId))
                {
                    return TypedResults.Forbid(authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
                }

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
                    return TypedResults.BadRequest(new OpenIddictResponse
                    {
                        Error = Errors.InvalidScope,
                        ErrorDescription = "The requested scope is not permitted."
                    });
                }

                // 4. Attach permissions to the final principal destination
                var principal = new ClaimsPrincipal(identity);
                principal.SetScopes(grantedScopes);

                // OpenIddict takes this principal and signs it into a secure JWT
                return TypedResults.SignIn(principal, authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            return TypedResults.BadRequest(new OpenIddictResponse
            {
                Error = Errors.UnsupportedGrantType,
                ErrorDescription = "The specified grant type is not supported."
            });
        }
    }
}
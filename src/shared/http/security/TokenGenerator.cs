
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace shared.http.security;

public interface ITokenGenerator
{
    Task<string> GenerateToken(string username, string role);
    Task<string> GenerateAnonymousToken();
}

public class AnonymousTokenGenerator : ITokenGenerator
{
    private static string AppName = "DjApp";
    private static string ClientName = "AnonymousClient";
    private readonly JwtConfiguration _configuration;

    public AnonymousTokenGenerator(IOptions<JwtConfiguration> configuration)
    {
        _configuration = configuration.Value;
    }

    public async Task<string> GenerateToken(string userName, string role)
    {
        return await GenerateAnonymousToken();
    }

    public async Task<string> GenerateAnonymousToken()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, ClientName),
            new Claim(ClaimTypes.Role, AppName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _configuration.Issuer,
            audience: _configuration.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(2),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

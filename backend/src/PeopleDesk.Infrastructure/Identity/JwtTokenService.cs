using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PeopleDesk.Application.Auth;
using PeopleDesk.Application.Common;

namespace PeopleDesk.Infrastructure.Identity;

public class JwtTokenService : ITokenIssuer
{
    private readonly IConfiguration _config;
    private readonly IOptions<AuthOptions> _auth;

    public JwtTokenService(IConfiguration config, IOptions<AuthOptions> auth)
    {
        _config = config;
        _auth = auth;
    }

    public (string Token, DateTime ExpiresAtUtc) IssueAccessToken(
        Guid userId, Guid organizationId, string email, Guid? employeeId, bool isPlatformAdmin, IEnumerable<string> permissions)
    {
        var key = ResolveSigningKey();
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(_auth.Value.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new("org", organizationId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString())
        };
        if (employeeId is { } emp) claims.Add(new Claim("employee", emp.ToString()));
        if (isPlatformAdmin) claims.Add(new Claim("platform_admin", "true"));
        claims.AddRange(permissions.Select(p => new Claim("perm", p)));

        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"] ?? "peopledesk",
            audience: _config["Jwt:Audience"] ?? "peopledesk-web",
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    public static string Sha256Hex(string raw) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();

    private SymmetricSecurityKey ResolveSigningKey()
    {
        var secret = _config["Jwt:Secret"];
        if (string.IsNullOrWhiteSpace(secret) || secret.Length < 32)
            throw new InvalidOperationException(
                "Jwt:Secret is missing or too short (min 32 chars). Configure it via environment (JWT__SECRET).");
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
    }
}

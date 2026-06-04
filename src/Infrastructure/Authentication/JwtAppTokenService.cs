using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OsuStocks.Domain.Common.Enums;
using OsuStocks.Domain.OsuIntegration.Interfaces;
using OsuStocks.Domain.OsuIntegration.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace OsuStocks.Infrastructure.Authentication;

internal sealed class JwtAppTokenService(IOptions<JwtOptions> options) : IAppTokenService
{
    private readonly JwtOptions _options = options.Value;

    public AppAuthToken CreateToken(Guid userId, long osuUserId, string username, UserRole role)
    {
        if (string.IsNullOrWhiteSpace(_options.SigningKey) || _options.SigningKey.Length < 32)
        {
            throw new InvalidOperationException("Jwt:SigningKey must be at least 32 characters.");
        }

        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddMinutes(Math.Max(_options.ExpirationMinutes, 1));

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, username),
            new(ClaimTypes.Role, role.ToString()),
            new("osu_user_id", osuUserId.ToString())
        };

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var signingCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var tokenDescriptor = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: signingCredentials);

        var token = new JwtSecurityTokenHandler().WriteToken(tokenDescriptor);

        return new AppAuthToken(token, expiresAt);
    }
}

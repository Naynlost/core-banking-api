using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Banking.Infrastructure.Identity;

public sealed record AuthToken(string AccessToken, DateTimeOffset ExpiresAtUtc);

public interface IJwtTokenGenerator
{
    AuthToken CreateToken(ApplicationUser user);
}

internal sealed class JwtTokenGenerator(
    IOptions<JwtOptions> options,
    IOptions<FraudReviewOptions> fraudReview,
    TimeProvider timeProvider) : IJwtTokenGenerator
{
    public AuthToken CreateToken(ApplicationUser user)
    {
        var jwt = options.Value;
        var now = timeProvider.GetUtcNow();
        var expiresAt = now.AddMinutes(jwt.AccessTokenMinutes);

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
            SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        if (fraudReview.Value.ReviewerEmails.Contains(user.Email ?? string.Empty, StringComparer.OrdinalIgnoreCase))
        {
            claims.Add(new Claim(ClaimTypes.Role, Banking.Application.Fraud.FraudReview.ReviewerRole));
        }

        var token = new JwtSecurityToken(
            issuer: jwt.Issuer,
            audience: jwt.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return new AuthToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}

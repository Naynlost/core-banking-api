using System.Security.Cryptography;
using System.Text;
using Banking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Banking.Infrastructure.Identity;

public sealed record IssuedRefreshToken(string Token, DateTimeOffset ExpiresAtUtc);

/// <summary>Outcome of presenting a refresh token: the user and a replacement token, or nothing.</summary>
public sealed record RefreshRotation(ApplicationUser User, IssuedRefreshToken NewToken);

public interface IRefreshTokenService
{
    Task<IssuedRefreshToken> IssueAsync(ApplicationUser user, CancellationToken cancellationToken);

    /// <summary>
    /// Consumes the presented token and issues a replacement. Returns null when
    /// the token is unknown, expired or already used — in the last case every
    /// active token of that user is revoked, because a used token showing up
    /// again means it leaked.
    /// </summary>
    Task<RefreshRotation?> RotateAsync(string token, CancellationToken cancellationToken);
}

internal sealed class RefreshTokenService(
    BankingDbContext context,
    IOptions<JwtOptions> options,
    TimeProvider timeProvider) : IRefreshTokenService
{
    public async Task<IssuedRefreshToken> IssueAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var issued = await StageNewTokenAsync(user.Id, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        return issued;
    }

    public async Task<RefreshRotation?> RotateAsync(string token, CancellationToken cancellationToken)
    {
        var hash = Hash(token);
        var stored = await context.Set<RefreshToken>()
            .SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (stored is null)
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        if (!stored.IsActive(now))
        {
            // Reuse of a consumed/expired token: assume the token leaked and cut
            // off every session of this user.
            await context.Set<RefreshToken>()
                .Where(t => t.UserId == stored.UserId && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), cancellationToken);
            return null;
        }

        var user = await context.Users.SingleOrDefaultAsync(u => u.Id == stored.UserId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        stored.RevokedAt = now;
        var issued = await StageNewTokenAsync(stored.UserId, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return new RefreshRotation(user, issued);
    }

    private async Task<IssuedRefreshToken> StageNewTokenAsync(string userId, CancellationToken cancellationToken)
    {
        // 256 bits of randomness; the client gets the value, the database its hash.
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var now = timeProvider.GetUtcNow();
        var expiresAt = now.AddDays(options.Value.RefreshTokenDays);

        await context.Set<RefreshToken>().AddAsync(
            new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TokenHash = Hash(token),
                CreatedAt = now,
                ExpiresAt = expiresAt,
            },
            cancellationToken);

        return new IssuedRefreshToken(token, expiresAt);
    }

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

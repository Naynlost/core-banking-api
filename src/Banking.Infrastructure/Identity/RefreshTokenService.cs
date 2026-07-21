using System.Security.Cryptography;
using System.Text;
using Banking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Banking.Infrastructure.Identity;

public sealed record IssuedRefreshToken(string Token, DateTimeOffset ExpiresAtUtc);

public sealed record RefreshRotation(ApplicationUser User, IssuedRefreshToken NewToken);

public interface IRefreshTokenService
{
    Task<IssuedRefreshToken> IssueAsync(ApplicationUser user, CancellationToken cancellationToken);

    // Bilinmeyen/süresi dolmuş/kullanılmış token null döner; kullanılmış token tekrar gelirse tüm token'lar iptal edilir
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
            // Kullanılmış/süresi dolmuş token tekrar geldi: sızmış varsayılıp kullanıcının tüm oturumları kesilir
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
        // 256 bit rastgelelik; istemci değeri, veritabanı hash'ini alır
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

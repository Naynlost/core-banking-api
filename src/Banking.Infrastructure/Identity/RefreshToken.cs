namespace Banking.Infrastructure.Identity;

// Sadece SHA-256 hash'i saklanır; iptal edilmiş/süresi dolmuş token'ın tekrar gelmesi reuse sayılır ve
// kullanıcının tüm aktif token'larını iptal eder
public sealed class RefreshToken
{
    public required Guid Id { get; init; }

    public required string UserId { get; init; }

    public required string TokenHash { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }

    public DateTimeOffset? RevokedAt { get; set; }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && now < ExpiresAt;
}

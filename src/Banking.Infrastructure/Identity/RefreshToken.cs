namespace Banking.Infrastructure.Identity;

/// <summary>
/// One issued refresh token. Only the SHA-256 hash is stored — a database leak
/// must not leak usable tokens. Rotation: using a token revokes it and issues a
/// replacement; presenting a revoked or expired token counts as reuse and kills
/// every active token of the user.
/// </summary>
public sealed class RefreshToken
{
    public required Guid Id { get; init; }

    public required string UserId { get; init; }

    /// <summary>Hex SHA-256 of the token value handed to the client.</summary>
    public required string TokenHash { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }

    public DateTimeOffset? RevokedAt { get; set; }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && now < ExpiresAt;
}

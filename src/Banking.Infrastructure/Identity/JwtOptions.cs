namespace Banking.Infrastructure.Identity;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = string.Empty;

    public string Audience { get; init; } = string.Empty;

    /// <summary>Never stored in appsettings; supplied via the Jwt__Secret environment variable.</summary>
    public string Secret { get; init; } = string.Empty;

    public int AccessTokenMinutes { get; init; } = 30;

    public int RefreshTokenDays { get; init; } = 7;
}

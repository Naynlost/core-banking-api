namespace Banking.Infrastructure.Identity;

// Demo amaçlı config-driven; gerçek bir sistemde rol atama admin UI/operasyonel süreçle yapılırdı
public sealed class FraudReviewOptions
{
    public const string SectionName = "FraudReview";

    public List<string> ReviewerEmails { get; init; } = [];
}

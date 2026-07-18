namespace Banking.Infrastructure.Identity;

/// <summary>
/// Which users carry the fraud-reviewer role, by email. Config-driven so the
/// demo needs no admin UI; a real deployment would assign roles through an
/// operational process instead of configuration.
/// </summary>
public sealed class FraudReviewOptions
{
    public const string SectionName = "FraudReview";

    public List<string> ReviewerEmails { get; init; } = [];
}

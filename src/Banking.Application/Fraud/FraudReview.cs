namespace Banking.Application.Fraud;

/// <summary>
/// The fraud review flow is a back-office concern: it is gated by a role, not
/// by account ownership like the customer endpoints.
/// </summary>
public static class FraudReview
{
    /// <summary>JWT role required to list and resolve fraud alerts.</summary>
    public const string ReviewerRole = "fraud-reviewer";
}

public static class FraudReviewErrors
{
    public const string NotFound = "fraud_alert.not_found";
    public const string InvalidStatusFilter = "fraud_alert.invalid_status_filter";
    public const string NoteTooLong = "fraud_alert.note_too_long";
    public const string PageOutOfRange = "fraud_alert.page_out_of_range";
    public const string PageSizeOutOfRange = "fraud_alert.page_size_out_of_range";
}

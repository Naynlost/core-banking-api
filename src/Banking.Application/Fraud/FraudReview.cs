namespace Banking.Application.Fraud;

// Müşteri endpoint'lerinden farklı olarak hesap sahipliği değil, rol bazlı yetkilendirilir
public static class FraudReview
{
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

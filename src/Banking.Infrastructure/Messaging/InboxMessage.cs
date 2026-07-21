namespace Banking.Infrastructure.Messaging;

// Consumer'ın bir mesajı işlediğini kaydeder; broker en az bir kez teslim ettiğinden tekrar işleme no-op olur
public sealed class InboxMessage
{
    public required string Consumer { get; init; }

    public required Guid MessageId { get; init; }

    public required DateTimeOffset ProcessedAt { get; init; }
}

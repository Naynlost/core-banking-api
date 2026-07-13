namespace Banking.Infrastructure.Messaging;

/// <summary>
/// Records that a consumer already processed a message. The broker delivers
/// at least once (the publisher may crash between publish and mark-processed),
/// so consumers check this table to make reprocessing a no-op.
/// </summary>
public sealed class InboxMessage
{
    public required string Consumer { get; init; }

    public required Guid MessageId { get; init; }

    public required DateTimeOffset ProcessedAt { get; init; }
}

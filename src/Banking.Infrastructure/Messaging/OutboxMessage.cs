namespace Banking.Infrastructure.Messaging;

/// <summary>
/// A domain event awaiting publication. Written in the same database transaction
/// as the change that raised it; a background publisher delivers it to the broker
/// at least once and marks it processed. Failed rows keep their error and are
/// retried on the next pass.
/// </summary>
public sealed class OutboxMessage
{
    public required Guid Id { get; init; }

    /// <summary>Event type name, e.g. "MoneyTransferred".</summary>
    public required string Type { get; init; }

    /// <summary>JSON-serialized event.</summary>
    public required string Payload { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }

    public DateTimeOffset? ProcessedAt { get; set; }

    public int Attempts { get; set; }

    public string? LastError { get; set; }

    /// <summary>W3C traceparent of the operation that raised the event, so the
    /// asynchronous publish/consume spans join the originating trace.</summary>
    public string? TraceParent { get; init; }

    /// <summary>Correlation id of the originating request, carried into consumer logs.</summary>
    public string? CorrelationId { get; init; }
}

namespace Banking.Infrastructure.Messaging;

// Olayı yaratan değişiklikle aynı transaction'da yazılır; publisher en az bir kez teslim edip işaretler
public sealed class OutboxMessage
{
    public required Guid Id { get; init; }

    public required string Type { get; init; }

    public required string Payload { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }

    public DateTimeOffset? ProcessedAt { get; set; }

    public int Attempts { get; set; }

    public string? LastError { get; set; }

    // Asenkron publish/consume span'leri orijinal trace'e katılabilsin diye
    public string? TraceParent { get; init; }

    public string? CorrelationId { get; init; }
}

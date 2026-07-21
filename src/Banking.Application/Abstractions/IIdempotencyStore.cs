namespace Banking.Application.Abstractions;

// Kullanıcı bazlı benzersiz key, işlemle aynı transaction'da yazılır; eş zamanlı aynı key ikinci insert'i düşürür
public sealed record IdempotencyRecord(
    string Key,
    string UserId,
    Guid TransactionId,
    DateTimeOffset CreatedAt);

public interface IIdempotencyStore
{
    Task<IdempotencyRecord?> GetAsync(string key, string userId, CancellationToken cancellationToken);

    // Kayıt burada sadece hazırlanır, UnitOfWork ile işlemle birlikte kalıcı olur
    Task AddAsync(IdempotencyRecord record, CancellationToken cancellationToken);
}

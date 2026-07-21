namespace Banking.Application.Abstractions;

// Repository'ler değişikliği sadece hazırlar, bu çağrılana kadar DB'ye hiçbir şey yazılmaz
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

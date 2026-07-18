using Banking.Domain.Accounts;

namespace Banking.Infrastructure.Persistence;

/// <summary>
/// Read-model row: running debit/credit totals per account, maintained by
/// <see cref="BankingDbContext"/> in the same transaction as every ledger
/// write. Purely derived data — it can always be rebuilt from ledger_entries
/// (the initial migration backfills it exactly that way). Lost-update races
/// are impossible because every movement also bumps the account's optimistic
/// concurrency token, which serializes writers per account.
/// </summary>
internal sealed class AccountBalance
{
    public required AccountId AccountId { get; init; }

    public decimal Debits { get; set; }

    public decimal Credits { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

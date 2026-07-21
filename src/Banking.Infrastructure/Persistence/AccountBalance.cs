using Banking.Domain.Accounts;

namespace Banking.Infrastructure.Persistence;

// Hesap başına debit/credit toplamı; BankingDbContext ledger yazımıyla aynı transaction'da günceller.
// Tamamen türetilmiş veri, ledger_entries'ten yeniden oluşturulabilir.
internal sealed class AccountBalance
{
    public required AccountId AccountId { get; init; }

    public decimal Debits { get; set; }

    public decimal Credits { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

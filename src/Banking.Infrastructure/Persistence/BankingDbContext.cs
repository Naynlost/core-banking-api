using Banking.Domain.Accounts;
using Banking.Domain.Fraud;
using Banking.Domain.Ledgers;
using Banking.Domain.StandingOrders;
using Banking.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Banking.Infrastructure.Persistence;

public sealed class BankingDbContext(DbContextOptions<BankingDbContext> options, TimeProvider timeProvider)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<Transaction> Transactions => Set<Transaction>();

    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();

    public DbSet<FraudAlert> FraudAlerts => Set<FraudAlert>();

    public DbSet<StandingOrder> StandingOrders => Set<StandingOrder>();

    internal DbSet<AccountBalance> AccountBalances => Set<AccountBalance>();

    // Ledger yazan her save, aynı transaction'da account_balances'ı da günceller; projeksiyon asla geride kalmaz
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await ProjectNewLedgerEntriesAsync(cancellationToken);
        return await base.SaveChangesAsync(cancellationToken);
    }

    private async Task ProjectNewLedgerEntriesAsync(CancellationToken cancellationToken)
    {
        var added = ChangeTracker.Entries<LedgerEntry>()
            .Where(entry => entry.State == EntityState.Added)
            .Select(entry => entry.Entity)
            .ToList();

        if (added.Count == 0)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        foreach (var group in added.GroupBy(entry => entry.AccountId))
        {
            var balance = await AccountBalances.FindAsync([group.Key], cancellationToken);
            if (balance is null)
            {
                balance = new AccountBalance { AccountId = group.Key };
                await AccountBalances.AddAsync(balance, cancellationToken);
            }

            foreach (var entry in group)
            {
                if (entry.Direction == EntryDirection.Debit)
                {
                    balance.Debits += entry.Amount.Amount;
                }
                else
                {
                    balance.Credits += entry.Amount.Amount;
                }
            }

            balance.UpdatedAt = now;
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BankingDbContext).Assembly);
    }
}

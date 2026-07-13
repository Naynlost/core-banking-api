using Banking.Domain.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Banking.Infrastructure.Persistence.Configurations;

internal sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("accounts");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasConversion(ValueConverters.AccountId)
            .ValueGeneratedNever();

        builder.Property(a => a.Owner)
            .HasMaxLength(200);

        builder.Property(a => a.Currency)
            .HasConversion(ValueConverters.Currency)
            .HasMaxLength(3);

        builder.Property(a => a.Type)
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(a => a.Status)
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(a => a.KycStatus)
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(a => a.DailyTransferLimit)
            .HasPrecision(18, 2);

        // App-managed optimistic concurrency token (Postgres has no rowversion type):
        // every movement bumps it, so concurrent movements on the same account
        // turn into an update conflict instead of silently coexisting.
        builder.Property(a => a.Version)
            .IsConcurrencyToken();
    }
}
